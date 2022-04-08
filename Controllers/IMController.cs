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
    private PhysicalFileProvider originalRepo;
    private PhysicalFileProvider convertedRepo;
    private CheckedFileContext db = new CheckedFileContext();
    private Crc32 crc32 = new Crc32();

    public IMController(ILogger<IMController> logger, IWebHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
        this.originalRepo = new PhysicalFileProvider(Path.Combine(this.env.WebRootPath, "images", "orig"));
        this.convertedRepo = new PhysicalFileProvider(Path.Combine(this.env.WebRootPath, "images", "out"));

        logger.LogInformation($"Database path: {db.DbPath}.");

        foreach (var f in this.originalRepo.GetDirectoryContents(""))
        {
            if (GetFormatInformation(f) != null)
            {
                var originalFile = checkFileHasChanged(f);
                processConverts(originalFile);
            }
        };

        db.SaveChanges();
    }

    [HttpGet(Name = "GetInfo")]
    public IEnumerable<OriginalFile> Get()
    {
        return db.OriginalFiles.AsEnumerable();
    }

    private IMagickFormatInfo? GetFormatInformation(IFileInfo file)
    {
        return MagickNET.GetFormatInformation(file.PhysicalPath)
            ?? MagickNET.GetFormatInformation(new MagickImageInfo(file.PhysicalPath).Format);
    }

    private void processConverts(OriginalFile originalFile)
    {
        _ = originalFile.FileName ?? throw new NullReferenceException(nameof(originalFile.FileName));
        String currentConversion = "800x600";
        foreach (ConvertedFile cf in originalFile.convertedFiles)
        {
            this.convertedRepo.GetFileInfo(Util.GenerateConvertedFileName(originalFile.FileName, currentConversion));

        }

    }

    private OriginalFile checkFileHasChanged(IFileInfo file)
    {
        String hash = String.Empty;
        using (Stream fs = file.CreateReadStream())
        {
            var bytes = crc32.ComputeHash(fs);
            foreach (byte b in bytes) hash += b.ToString("x2").ToLower();

            logger.LogInformation("CRC-32 is {0}", hash);
            Util.GetInfoFromFileName(Path.GetFileName(file.PhysicalPath), out string name, out uint num, out string type);

            var originalFile = db.OriginalFiles.SingleOrDefault(c => c.FileName == name) ?? new OriginalFile
            {
                FileName = name,
                Artikelnummer = num,
                FileType = type,
                FileLength = file.Length,
                FileCrc = Crc32.getUIntResult(bytes),
                WebURL = new Uri("/img/orig/" + Path.GetFileName(file.PhysicalPath), UriKind.Relative)
            };

            Util.AddOrUpdate(db, originalFile, logger);
            return originalFile;
        }
    }

    private void ConvertImageFromOneFormatToAnother(IFileInfo file, String conversionName)
    {
        // Read first frame of gif image
        using (var image = new MagickImage(file.PhysicalPath))
        {
            // Save frame as jpg
            var fn = Path.GetFileNameWithoutExtension(file.PhysicalPath);
            var outfile = convertedRepo.GetFileInfo(fn + "_" + conversionName + ".png");
            image.Write(outfile.PhysicalPath);
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
