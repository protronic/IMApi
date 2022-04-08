using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using ImageMagick;
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
        var fileName = Util.getFileName(originalFile);
        var convertedFileInfo = this.convertedRepo.GetFileInfo(fileName);
        var convertedFile = originalFile.convertedFiles.SingleOrDefault(c => c.FileName == fileName);
        if (convertedFile == null)
        {
            ConvertImageFromOneFormatToAnother(fileName, currentConversion);
            Util.checkFile(convertedFileInfo, logger, out string name, out string num, out string type, out uint crc);
            originalFile.convertedFiles.Add(new ConvertedFile
            {
                FileName = name,
                ConversionType = currentConversion,
                FileType = type,
                FileCrc = crc,
                FileLength = convertedFileInfo.Length,
                WebURL = new Uri("/img/out/" + Path.GetFileName(convertedFileInfo.PhysicalPath), UriKind.Relative)
            });
        }
    }

    private OriginalFile checkFileHasChanged(IFileInfo file)
    {
        Util.checkFile(file, logger, out string name, out string num, out string type, out uint crc);
        var originalFile = db.OriginalFiles.SingleOrDefault(c => c.FileName == name) ?? new OriginalFile
        {
            FileName = name,
            Artikelnummer = num,
            FileType = type,
            FileLength = file.Length,
            FileCrc = crc,
            WebURL = new Uri("/img/orig/" + Path.GetFileName(file.PhysicalPath), UriKind.Relative)
        };
        Util.AddOrUpdate(db, originalFile, logger);
        return originalFile;
    }

    private void ConvertImageFromOneFormatToAnother(string srcfile, String conversionName, String fileType = "png")
    {
        // Read first frame of gif image
        using (var image = new MagickImage(originalRepo.GetFileInfo(srcfile).PhysicalPath))
        {
            // Save frame as jpg
            var outfile = convertedRepo.GetFileInfo(Path.Combine(conversionName, srcfile) + "." + fileType);
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
