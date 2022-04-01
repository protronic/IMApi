using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using ImageMagick;
using DamienG.Security.Cryptography;
using Protronic.CeckedFileInfo;

namespace IMApi.Controllers;

[ApiController]
[Route("[controller]")]
public class IMController : Controller
{
    private readonly ILogger<IMController> logger;
    private IWebHostEnvironment env;
    private PhysicalFileProvider imgRepo;
    private CheckedFileContext db = new CheckedFileContext();
    private Crc32 crc32 = new Crc32();

    public IMController(ILogger<IMController> logger, IWebHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
        this.imgRepo = new PhysicalFileProvider(Path.Combine(this.env.WebRootPath, "images"));
        logger.LogInformation($"Database path: {db.DbPath}.");
    }

    [HttpGet(Name = "GetInfo")]
    public IEnumerable<CheckedFile> Get()
    {
        var listFiles = (from f in this.imgRepo.GetDirectoryContents("")
                         where GetFormatInformation(f) != null
                         select checkFiles(f)
                ).ToArray();
        db.SaveChanges();
        
        return listFiles;
    }

    private static IMagickFormatInfo? GetFormatInformation(IFileInfo file)
    {
        IMagickFormatInfo? info = MagickNET.GetFormatInformation(file.PhysicalPath);
        if (info == null)
            info = MagickNET.GetFormatInformation(new MagickImageInfo(file.PhysicalPath).Format);




        return info;
    }

    private CheckedFile checkFiles(IFileInfo file)
    {
        String hash = String.Empty;
        using (Stream fs = file.CreateReadStream())
            foreach (byte b in crc32.ComputeHash(fs)) hash += b.ToString("x2").ToLower();

        logger.LogInformation("CRC-32 is {0}", hash);
        var cf = new CheckedFile { FileName = file.Name, FileLength=file.Length, FileCrcId = crc32.hash };
        this.db.Add(cf);
        return cf;
    }

    private void ConvertImageFromOneFormatToAnother(IFileInfo file)
    {
        // Read first frame of gif image
        using (var image = new MagickImage(file.PhysicalPath))
        {
            // Save frame as jpg
            image.Write(this.imgRepo.GetFileInfo("out/" + file.Name + ".png").PhysicalPath);
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
    }
}
