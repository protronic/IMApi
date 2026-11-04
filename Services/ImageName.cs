using System.Text.RegularExpressions;

namespace IMApi.Services;

/// <summary>
/// What the file name of an original image says about it. Product images follow the
/// convention <c>{Artikelnummer}[_{Index}][_{Sprache}].{ext}</c>, e.g. <c>1017000034.png</c>,
/// <c>1017000034_1.png</c> or <c>1017000034_1_EN.png</c>.
/// </summary>
/// <param name="FileName">The original file name.</param>
/// <param name="Name">Name of the converted files without extension: Artikelnummer plus index
/// (the language becomes a folder), or the original base name for other files.</param>
/// <param name="Artikelnummer">Article number, null when the name does not follow the convention.</param>
/// <param name="Language">Language from the file name suffix, null when the image is language neutral.</param>
public record ImageName(string FileName, string Name, string? Artikelnummer, string? Language)
{
    private static readonly Regex Pattern = new(
        @"^(?<num>\d+)(?:_(?<index>\d+))?(?:_(?<lang>[A-Za-z]{2,3}))?\.(?<ext>[A-Za-z0-9]+)$",
        RegexOptions.CultureInvariant);

    public static ImageName Parse(string fileName, IReadOnlyCollection<string> languages)
    {
        var baseName = Path.GetFileName(fileName);
        var match = Pattern.Match(baseName);
        if (!match.Success)
            return new ImageName(baseName, Path.GetFileNameWithoutExtension(baseName), null, null);

        var num = match.Groups["num"].Value;
        var name = match.Groups["index"].Success ? $"{num}_{match.Groups["index"].Value}" : num;
        string? lang = null;
        if (match.Groups["lang"].Success)
        {
            lang = match.Groups["lang"].Value.ToUpperInvariant();
            if (!languages.Contains(lang, StringComparer.OrdinalIgnoreCase))
                // An unknown suffix is part of the name, not a language.
                return new ImageName(baseName, Path.GetFileNameWithoutExtension(baseName), num, null);
        }
        return new ImageName(baseName, name, num, lang);
    }
}
