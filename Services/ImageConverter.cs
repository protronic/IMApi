using IMApi.Options;
using ImageMagick;
using Microsoft.Extensions.Options;

namespace IMApi.Services;

/// <summary>
/// Converts product images with Magick.NET: resize, soft alpha edge, drop shadow on the
/// preset background and an optional text at the bottom - the same pipeline as the former
/// folder-based service.
/// </summary>
public class ImageConverter
{
    private readonly ImApiOptions options;
    private readonly SemaphoreSlim parallel;

    public ImageConverter(IOptions<ImApiOptions> options)
    {
        this.options = options.Value;
        parallel = new SemaphoreSlim(Math.Max(1, this.options.MaxParallelConversions));
    }

    public async Task<byte[]> ConvertAsync(byte[] source, ConversionPreset preset, string? text, CancellationToken ct)
    {
        await parallel.WaitAsync(ct);
        try
        {
            return Convert(source, preset, text);
        }
        finally
        {
            parallel.Release();
        }
    }

    private byte[] Convert(byte[] source, ConversionPreset preset, string? text)
    {
        if (!Enum.TryParse<MagickFormat>(preset.Format, true, out var format))
            throw new ArgumentException($"Unbekanntes Ausgabeformat '{preset.Format}' im Preset '{preset.Name}'");

        // Only the first frame of animated images is used.
        using var image = new MagickImage(source);
        image.Strip();
        image.Quality = 100;
        if (preset.Width > 0 && preset.Height > 0) image.Resize(preset.Width, preset.Height);

        // -background black -shadow 100x10+0+0, on the preset background
        using var shadow = new MagickImage(image.Clone());
        shadow.Quality = 100;
        shadow.Shadow(0, 0, 40.0 * image.Width / 2000.0, new Percentage(90), MagickColors.Black);
        shadow.BackgroundColor = new MagickColor(preset.Background);

        // -alpha set -virtual-pixel transparent -channel A -blur -level 50%,100% +channel
        image.Alpha(AlphaAction.Set);
        image.VirtualPixelMethod = VirtualPixelMethod.Transparent;
        image.Blur(0, 6.0 * image.Width / 2000.0, Channels.Alpha);
        image.Level(new Percentage(50), new Percentage(100), Channels.Alpha);

        if (!string.IsNullOrWhiteSpace(text))
        {
            var settings = new MagickReadSettings
            {
                Font = options.LabelFont,
                TextGravity = Gravity.South,
                FillColor = MagickColors.White,
                TextUnderColor = new MagickColor("#00000040"),
                BackgroundColor = MagickColors.Transparent,
                Height = image.Height,
                Width = image.Width,
            };
            using var caption = new MagickImage($"label:{EscapeLabel(text)}", settings);
            image.Composite(caption, Gravity.South, CompositeOperator.Over);
        }

        using var images = new MagickImageCollection();
        images.Add(shadow.Clone());
        images.Add(image.Clone());
        using var merged = images.Merge(); // -layers merge
        return merged.ToByteArray(format);
    }

    /// <summary>
    /// ImageMagick reads "label:@file" from a file and expands %-escapes in label text;
    /// texts come from users, so both are neutralised.
    /// </summary>
    public static string EscapeLabel(string text)
    {
        var escaped = text.Trim().Replace("%", "%%");
        return escaped.StartsWith('@') ? "\\" + escaped : escaped;
    }

    public static string ContentType(string format) => format.ToLowerInvariant() switch
    {
        "png" => "image/png",
        "jpg" or "jpeg" => "image/jpeg",
        "webp" => "image/webp",
        "gif" => "image/gif",
        "tif" or "tiff" => "image/tiff",
        "bmp" => "image/bmp",
        _ => "application/octet-stream",
    };
}
