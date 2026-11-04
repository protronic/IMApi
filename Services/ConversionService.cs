using IMApi.Data;
using IMApi.Options;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IMApi.Services;

public record FileRef(string SpaceId, string Path);

public class ConvertRequest
{
    /// <summary>Original images in OpenCloud: space id and path inside the space.</summary>
    public List<FileRef> Files { get; set; } = [];

    /// <summary>Preset names; empty uses the preselected presets.</summary>
    public List<string>? Presets { get; set; }

    /// <summary>Languages to produce; empty uses the file name suffix or, without one, all languages.</summary>
    public List<string>? Languages { get; set; }

    /// <summary>Render the image text (stored per Artikelnummer, or from <see cref="Texts"/>) into the images.</summary>
    public bool ApplyText { get; set; } = true;

    /// <summary>Texts per language for this conversion, overriding the stored ones.</summary>
    public Dictionary<string, string>? Texts { get; set; }

    /// <summary>Output folder, relative to the original's folder or, starting with "/", to the space root.</summary>
    public string? OutputFolder { get; set; }
}

public record ConvertedOutput(string Preset, string Language, string Path, bool WithText);

public record ConversionResult(string SpaceId, string Path, string? Artikelnummer, List<ConvertedOutput> Outputs, string? Error);

public class ConversionService(
    OpenCloudClient openCloud,
    ImageConverter converter,
    ImApiDbContext db,
    IOptions<ImApiOptions> options,
    ILogger<ConversionService> logger)
{
    private readonly ImApiOptions options = options.Value;

    /// <summary>Checks the request against the configuration; returns an error message or null.</summary>
    public string? Validate(ConvertRequest request)
    {
        if (request.Files.Count == 0) return "Keine Dateien angegeben";
        if (request.Files.Count > 200) return "Höchstens 200 Dateien pro Auftrag";
        var unknownPresets = (request.Presets ?? []).Except(options.EffectivePresets.Select(p => p.Name)).ToList();
        if (unknownPresets.Count > 0) return "Unbekannte Presets: " + string.Join(", ", unknownPresets);
        var unknownLanguages = (request.Languages ?? []).Select(l => l.ToUpperInvariant())
            .Except(options.EffectiveLanguages).ToList();
        if (unknownLanguages.Count > 0) return "Unbekannte Sprachen: " + string.Join(", ", unknownLanguages);
        // The output folder depends on each file's location and is checked per file.
        return null;
    }

    public async Task<List<ConversionResult>> ConvertAsync(ConvertRequest request, string authorization, string user, CancellationToken ct)
    {
        var presets = request.Presets is { Count: > 0 }
            ? options.EffectivePresets.Where(p => request.Presets.Contains(p.Name)).ToList()
            : options.EffectivePresets.Where(p => p.Selected).ToList();
        var requestedLanguages = request.Languages?.Select(l => l.ToUpperInvariant()).ToList();
        var createdFolders = new HashSet<string>();
        var results = new List<ConversionResult>();

        foreach (var file in request.Files)
        {
            var image = ImageName.Parse(file.Path, options.EffectiveLanguages);
            var outputs = new List<ConvertedOutput>();
            try
            {
                var languages = requestedLanguages is { Count: > 0 } ? requestedLanguages
                    : image.Language is not null ? [image.Language]
                    : options.EffectiveLanguages.ToList();
                var texts = request.ApplyText ? await TextsFor(image, request.Texts, ct) : [];
                var outputFolder = ResolveOutputFolder(request.OutputFolder ?? options.DefaultOutputFolder, file.Path);

                var source = await openCloud.DownloadAsync(file.SpaceId, file.Path, options.MaxSourceBytes, authorization, ct);
                foreach (var preset in presets)
                {
                    var presetLanguages = preset.Languages.Count == 0
                        ? languages
                        : languages.Where(l => preset.Languages.Contains(l, StringComparer.OrdinalIgnoreCase));
                    foreach (var language in presetLanguages)
                    {
                        var text = preset.ApplyText ? texts.GetValueOrDefault(language) : null;
                        var converted = await converter.ConvertAsync(source, preset, text, ct);
                        var folder = $"{outputFolder}/{language}/{preset.Name}";
                        if (createdFolders.Add(file.SpaceId + folder))
                            await openCloud.EnsureFolderAsync(file.SpaceId, folder, authorization, ct);
                        var target = $"{folder}/{image.Name}.{preset.Format.ToLowerInvariant()}";
                        await openCloud.UploadAsync(file.SpaceId, target, converted,
                            ImageConverter.ContentType(preset.Format), authorization, ct);
                        outputs.Add(new ConvertedOutput(preset.Name, language, target, !string.IsNullOrWhiteSpace(text)));
                    }
                }
                logger.LogInformation("{User} converted {Path} into {Count} images", user, file.Path, outputs.Count);
                results.Add(new ConversionResult(file.SpaceId, file.Path, image.Artikelnummer, outputs, null));
            }
            catch (Exception e) when (e is OpenCloudException or MagickException or ArgumentException or HttpRequestException)
            {
                logger.LogWarning(e, "{User} could not convert {Path}", user, file.Path);
                var error = e is MagickException ? $"Bild kann nicht gelesen werden: {e.Message}" : e.Message;
                results.Add(new ConversionResult(file.SpaceId, file.Path, image.Artikelnummer, outputs, error));
            }
        }
        return results;
    }

    private async Task<Dictionary<string, string>> TextsFor(ImageName image, Dictionary<string, string>? overrides, CancellationToken ct)
    {
        var texts = image.Artikelnummer is null
            ? new Dictionary<string, string>()
            : await db.ImageTexts.Where(t => t.Artikelnummer == image.Artikelnummer)
                .ToDictionaryAsync(t => t.Language, t => t.Text, ct);
        foreach (var (language, text) in overrides ?? [])
            texts[language.ToUpperInvariant()] = text;
        return texts;
    }

    /// <summary>
    /// Folder for the converted images: relative to the folder of the original, or to the
    /// space root when it starts with "/". ".." is allowed but cannot leave the space.
    /// </summary>
    public static string ResolveOutputFolder(string folder, string originalPath)
    {
        folder = folder.Trim().Replace('\\', '/');
        if (folder.Length == 0) throw new ArgumentException("Ausgabeordner fehlt");
        var start = folder.StartsWith('/') ? "" : originalPath[..Math.Max(0, originalPath.TrimEnd('/').LastIndexOf('/'))];
        var segments = new List<string>();
        foreach (var segment in (start + "/" + folder).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0) throw new ArgumentException("Ausgabeordner liegt außerhalb des Space");
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }
        if (segments.Count == 0) throw new ArgumentException("Ausgabeordner darf nicht die Wurzel des Space sein");
        return "/" + string.Join('/', segments);
    }
}
