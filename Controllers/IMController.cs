using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using ImageMagick;

namespace IMApi.Controllers;

[ApiController]
[Route("[controller]")]
public class IMController : Controller
{
    private readonly ILogger<IMController> logger;
    private IWebHostEnvironment env;
    private MagickImageInfo imi;

    public IMController(ILogger<IMController> logger, IWebHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
        this.imi = new MagickImageInfo();
    }

    [HttpGet(Name = "GetInfo")]
    public IEnumerable<IMagickFormatInfo> Get()
    {
        var fileInfos = new List<IMagickFormatInfo>();
        var provider = new PhysicalFileProvider(Path.Combine(this.env.WebRootPath, "images"));
        foreach (PhysicalFileInfo file in provider.GetDirectoryContents(""))
        {
            var info = GetFormatInformation(new FileInfo(file.PhysicalPath));
            if (info != null)
                fileInfos.Add(info);
        }
        return fileInfos.AsEnumerable<IMagickFormatInfo>();
    }

    private static IMagickFormatInfo? GetFormatInformation(FileInfo file)
    {
        IMagickFormatInfo? info = MagickNET.GetFormatInformation(file);
        if (info != null)
            return info;

        MagickImageInfo imageInfo = new MagickImageInfo(file);
        return MagickNET.GetFormatInformation(imageInfo.Format);
    }

    public static void ConvertImageFromOneFormatToAnother()
    {
        // Read first frame of gif image
        using (var image = new MagickImage(SampleFiles.SnakewareGif))
        {
            // Save frame as jpg
            image.Write(SampleFiles.OutputDirectory + "Snakeware.jpg");
        }

        var settings = new MagickReadSettings();
        // Tells the xc: reader the image to create should be 800x600
        settings.Width = 800;
        settings.Height = 600;

        using (var memStream = new MemoryStream())
        {
            // Create image that is completely purple and 800x600
            using (var image = new MagickImage("xc:purple", settings))
            {
                // Sets the output format to png
                image.Format = MagickFormat.Png;

                // Write the image to the memorystream
                image.Write(memStream);
            }
        }

        // Read image from file
        using (var image = new MagickImage(SampleFiles.SnakewarePng))
        {
            // Sets the output format to jpeg
            image.Format = MagickFormat.Jpeg;

            // Create byte array that contains a jpeg file
            byte[] data = image.ToByteArray();
        }
    }

    public static void ConvertCmykToRgb()
    {
        // Uses sRGB.icm, eps/pdf produce better result when you set this before loading.
        var settings = new MagickReadSettings();
        settings.ColorSpace = ColorSpace.sRGB;

        // Read image from file
        using (var image = new MagickImage(SampleFiles.SnakewareJpg))
        {
            // Will use the CMYK profile if the image does not contain a color profile.
            // The second profile will transform the colorspace from CMYK to RGB
            image.TransformColorSpace(ColorProfile.USWebCoatedSWOP, ColorProfile.SRGB);

            // Save image as png
            image.Write(SampleFiles.OutputDirectory + "Snakeware.png");
        }

        // Read image from file
        using (var image = new MagickImage(SampleFiles.SnakewareJpg))
        {
            // Will use the CMYK profile if your image does not contain a color profile.
            // The second profile will transform the colorspace from your custom icc profile
            image.TransformColorSpace(ColorProfile.USWebCoatedSWOP, new ColorProfile(SampleFiles.YourProfileIcc));

            // Save image as tiff
            image.Write(SampleFiles.OutputDirectory + "Snakeware.tiff");
        }
    }
}
