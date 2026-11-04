using IMApi.Options;
using IMApi.Services;
using ImageMagick;
using Microsoft.Extensions.Options;

namespace IMApi.Tests;

public class ImageNameTests
{
    private static readonly string[] Languages = ["DE", "EN"];

    [Theory]
    [InlineData("1017000034.png", "1017000034", "1017000034", null)]
    [InlineData("1017000034_1.png", "1017000034_1", "1017000034", null)]
    [InlineData("1017000034_EN.jpg", "1017000034", "1017000034", "EN")]
    [InlineData("1017000034_2_de.PNG", "1017000034_2", "1017000034", "DE")]
    [InlineData("/Produkte/Bilder/0015000003.png", "0015000003", "0015000003", null)]
    public void ParsesProductImageNames(string file, string name, string artikelnummer, string? language)
    {
        var parsed = ImageName.Parse(file, Languages);
        Assert.Equal(name, parsed.Name);
        Assert.Equal(artikelnummer, parsed.Artikelnummer);
        Assert.Equal(language, parsed.Language);
    }

    [Theory]
    [InlineData("Foto vom Messestand.jpg", "Foto vom Messestand")]
    [InlineData("abc1017000034.png", "abc1017000034")]
    public void KeepsOtherNames(string file, string name)
    {
        var parsed = ImageName.Parse(file, Languages);
        Assert.Equal(name, parsed.Name);
        Assert.Null(parsed.Artikelnummer);
        Assert.Null(parsed.Language);
    }

    [Fact]
    public void UnknownLanguageSuffixIsPartOfTheName()
    {
        var parsed = ImageName.Parse("1017000034_FR.png", Languages);
        Assert.Equal("1017000034_FR", parsed.Name);
        Assert.Equal("1017000034", parsed.Artikelnummer);
        Assert.Null(parsed.Language);
    }
}

public class PathTests
{
    [Theory]
    [InlineData("konvertiert", "/Produkte/1.png", "/Produkte/konvertiert")]
    [InlineData("konvertiert", "/1.png", "/konvertiert")]
    [InlineData("../out", "/Produkte/orig/1.png", "/Produkte/out")]
    [InlineData("/Ausgabe/web", "/Produkte/1.png", "/Ausgabe/web")]
    [InlineData("./a//b/", "/P/1.png", "/P/a/b")]
    public void ResolvesOutputFolder(string folder, string original, string expected) =>
        Assert.Equal(expected, ConversionService.ResolveOutputFolder(folder, original));

    [Theory]
    [InlineData("../..", "/Produkte/1.png")]
    [InlineData("/", "/Produkte/1.png")]
    [InlineData("  ", "/Produkte/1.png")]
    public void RejectsFoldersOutsideTheSpace(string folder, string original) =>
        Assert.Throws<ArgumentException>(() => ConversionService.ResolveOutputFolder(folder, original));

    [Fact]
    public void EscapesWebDavUrls() =>
        Assert.Equal("dav/spaces/storage%24space%21id/Produkt%20Bilder/%C3%84%23.png",
            OpenCloudClient.DavUrl("storage$space!id", "/Produkt Bilder/Ä#.png"));

    [Theory]
    [InlineData("space", "/a/../b.png")]
    [InlineData("a/b", "/b.png")]
    [InlineData("", "/b.png")]
    public void RejectsInvalidWebDavPaths(string space, string path) =>
        Assert.Throws<ArgumentException>(() => OpenCloudClient.DavUrl(space, path));
}

public class ImageConverterTests
{
    private static readonly string Font = "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf";

    private static byte[] Fixture => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "1017000034_1.png"));

    private static ImageConverter Converter() =>
        new(Microsoft.Extensions.Options.Options.Create(new ImApiOptions { LabelFont = Font }));

    [Fact]
    public async Task ConvertsToPresetSize()
    {
        var preset = new ConversionPreset { Name = "web", Width = 500, Height = 500, Background = "#FFFFFFFF" };
        var result = await Converter().ConvertAsync(Fixture, preset, null, TestContext.Current.CancellationToken);

        using var image = new MagickImage(result);
        Assert.Equal(MagickFormat.Png, image.Format);
        // The image fits into 500x500; the shadow adds a small border.
        Assert.InRange(Math.Max(image.Width, image.Height), 500u, 560u);
    }

    [Fact]
    public async Task WritesTheRequestedFormat()
    {
        var preset = new ConversionPreset { Name = "small", Width = 100, Height = 100, Format = "jpg" };
        var result = await Converter().ConvertAsync(Fixture, preset, null, TestContext.Current.CancellationToken);
        using var image = new MagickImage(result);
        Assert.Equal(MagickFormat.Jpeg, image.Format);
    }

    [Fact]
    public async Task RendersText()
    {
        Assert.SkipUnless(File.Exists(Font), "Liberation fonts are not installed");
        var preset = new ConversionPreset { Name = "web", Width = 500, Height = 500 };
        var converter = Converter();
        var ct = TestContext.Current.CancellationToken;
        using var plain = new MagickImage(await converter.ConvertAsync(Fixture, preset, null, ct));
        using var labelled = new MagickImage(await converter.ConvertAsync(Fixture, preset, "Artikel 1017000034", ct));
        Assert.True(plain.Compare(labelled, ErrorMetric.RootMeanSquared) > 0.01);
    }

    [Fact]
    public async Task RejectsUnknownFormats()
    {
        var preset = new ConversionPreset { Name = "x", Format = "nope" };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Converter().ConvertAsync(Fixture, preset, null, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("@/etc/passwd", "\\@/etc/passwd")]
    [InlineData("100% Qualität", "100%% Qualität")]
    [InlineData("  Text  ", "Text")]
    public void EscapesLabelText(string text, string expected) =>
        Assert.Equal(expected, ImageConverter.EscapeLabel(text));
}
