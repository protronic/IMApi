namespace IMApi.Options;

/// <summary>
/// Settings of the service, bound from the "ImApi" configuration section
/// (appsettings.json, config/imapi.json or environment variables such as
/// <c>ImApi__OpenCloudUrl</c>).
/// </summary>
public class ImApiOptions
{
    public const string Section = "ImApi";

    /// <summary>
    /// Base URL of OpenCloud as seen from this service. Inside the compose stack this
    /// is the internal address of the OpenCloud proxy (no TLS between the containers).
    /// </summary>
    public string OpenCloudUrl { get; set; } = "http://opencloud:9200";

    /// <summary>Skip TLS certificate validation towards OpenCloud (self-signed certificates).</summary>
    public bool OpenCloudInsecure { get; set; }

    /// <summary>
    /// Path prefix under which the OpenCloud proxy forwards requests to this service
    /// (the proxy keeps the prefix). Empty when the service is called directly.
    /// </summary>
    public string PathBase { get; set; } = "/imapi";

    /// <summary>SQLite database file for the image texts.</summary>
    public string DatabasePath { get; set; } = "data/imapi.db";

    /// <summary>Font for image texts: a font name known to ImageMagick or a path to a font file.</summary>
    public string LabelFont { get; set; } = "Arial";

    /// <summary>
    /// Folder for converted images. Relative paths are resolved against the folder of the
    /// original image, paths starting with "/" against the root of the original's space.
    /// </summary>
    public string DefaultOutputFolder { get; set; } = "konvertiert";

    /// <summary>Languages images and texts can have. A file name suffix like "_EN" selects one.</summary>
    public List<string>? Languages { get; set; }

    /// <summary>Conversion presets. When not configured, <see cref="ConversionPreset.Defaults"/> is used.</summary>
    public List<ConversionPreset>? Presets { get; set; }

    /// <summary>Largest original image (bytes) that is downloaded for a conversion.</summary>
    public long MaxSourceBytes { get; set; } = 200L * 1024 * 1024;

    /// <summary>How many images are converted at the same time (memory bound).</summary>
    public int MaxParallelConversions { get; set; } = 2;

    public IReadOnlyList<string> EffectiveLanguages =>
        Languages is { Count: > 0 } ? Languages.Select(l => l.ToUpperInvariant()).ToList() : ["DE", "EN"];

    public IReadOnlyList<ConversionPreset> EffectivePresets =>
        Presets is { Count: > 0 } ? Presets : ConversionPreset.Defaults;
}

public class ConversionPreset
{
    /// <summary>Name of the preset; also the name of its output folder.</summary>
    public string Name { get; set; } = "";

    /// <summary>Target width and height in pixels (aspect ratio is kept). 0 keeps the original size.</summary>
    public uint Width { get; set; }
    public uint Height { get; set; }

    /// <summary>Output format, e.g. png, jpg, webp.</summary>
    public string Format { get; set; } = "png";

    /// <summary>Background colour behind the shadow, e.g. #FFFFFFFF (white) or #00000000 (transparent).</summary>
    public string Background { get; set; } = "#00000000";

    /// <summary>Whether the image text of the article is rendered into the image.</summary>
    public bool ApplyText { get; set; } = true;

    /// <summary>Languages this preset is produced for; empty means all languages.</summary>
    public List<string> Languages { get; set; } = [];

    /// <summary>Preselected in the conversion dialog.</summary>
    public bool Selected { get; set; } = true;

    /// <summary>
    /// The presets of the former folder-based service (web, thumb, 100x100, 200x200,
    /// 2000x2000, max) with their sizes, backgrounds and languages.
    /// </summary>
    public static IReadOnlyList<ConversionPreset> Defaults { get; } =
    [
        new() { Name = "web", Width = 500, Height = 500, Background = "#FFFFFFFF", Languages = ["DE", "EN"] },
        new() { Name = "thumb", Width = 100, Height = 100, Background = "#FFFFFFFF", Languages = ["DE"] },
        new() { Name = "100x100", Width = 100, Height = 100, Languages = ["DE", "EN"] },
        new() { Name = "200x200", Width = 200, Height = 200, Languages = ["DE", "EN"] },
        new() { Name = "2000x2000", Width = 2000, Height = 2000, Languages = ["DE"] },
        new() { Name = "max", Languages = ["DE"] },
    ];
}
