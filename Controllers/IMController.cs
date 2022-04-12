using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
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
    private List<OriginalFile> ofs;

    public IMController(ILogger<IMController> logger, IWebHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
        this.originalRepo = new PhysicalFileProvider(Path.Combine(this.env.WebRootPath, "images", "orig"));
        this.convertedRepo = new PhysicalFileProvider(Path.Combine(this.env.WebRootPath, "images", "out"));

        logger.LogInformation($"Database path: {db.DbPath}.");
        this.ofs = db.OriginalFiles.Include(o => o.convertedFiles).Include(o => o.conversions).ToList();
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
        return this.ofs;
    }

    private IMagickFormatInfo? GetFormatInformation(IFileInfo file)
    {
        return MagickNET.GetFormatInformation(file.PhysicalPath)
            ?? MagickNET.GetFormatInformation(new MagickImageInfo(file.PhysicalPath).Format);
    }

    private void processConverts(OriginalFile originalFile)
    {
        _ = originalFile.FileName ?? throw new NullReferenceException(nameof(originalFile.FileName));

        var fileName = Util.getFileName(originalFile);

        foreach (ConversionInfo con in originalFile.conversions)
        {
            var convertedFile = originalFile.convertedFiles.Where(c => con.Equals(c.Conversion)).SingleOrDefault();
            if (convertedFile != null && !convertedRepo.GetFileInfo(Util.getFileName(convertedFile)).Exists)
            {
                originalFile.convertedFiles.Remove(convertedFile);
                convertedFile = null;
            }

            if (convertedFile == null)
            {
                var convertedFileInfo = ConvertImageFromOneFormatToAnother(fileName, con);
                Util.checkFile(convertedFileInfo, logger, out string name, out string num, out Lang lang, out string type, out uint crc);
                originalFile.convertedFiles.Add(new ConvertedFile
                {
                    FileName = name,
                    Conversion = con,
                    FileType = type,
                    FileCrc = crc,
                    FileLength = convertedFileInfo.Length,
                    WebURL = new Uri("/img/out/" + con.ConversionName + "/" + fileName, UriKind.Relative)
                });
            }

        }
    }

    private OriginalFile checkFileHasChanged(IFileInfo file)
    {
        Util.checkFile(file, logger, out string name, out string num, out Lang lang, out string type, out uint crc);
        var originalFile = ofs.SingleOrDefault(c => c.FileName == name) ?? new OriginalFile
        {
            FileName = name,
            Artikelnummer = num,
            FileType = type,
            FileLength = file.Length,
            FileCrc = crc,
            conversions = new List<ConversionInfo>(Util.DEFAULT_CONVERSIONS),
            WebURL = new Uri("/img/orig/" + Path.GetFileName(file.PhysicalPath), UriKind.Relative)
        };
        Util.AddOrUpdate(db, originalFile, logger);
        return originalFile;
    }

    private IFileInfo ConvertImageFromOneFormatToAnother(string srcfile, ConversionInfo con)
    {
        IFileInfo outfile;
        _ = con.ConversionName ?? throw new NullReferenceException(nameof(con.ConversionName));
        var srcFilePath = originalRepo.GetFileInfo(srcfile).PhysicalPath;
        var conversionFilePath = Path.Combine(con.ConversionName, Path.GetFileNameWithoutExtension(srcFilePath)) + "." + con.FileType;
        outfile = convertedRepo.GetFileInfo(conversionFilePath);

        // Read first frame of gif image
        using (var image = new MagickImage(srcFilePath))
        using (FileStream fs = System.IO.File.Create(outfile.PhysicalPath))
        {
            // Save frame as jpg
            image.Write(fs);
            fs.Flush();
            outfile = convertedRepo.GetFileInfo(conversionFilePath);
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
        return outfile;
    }
}
