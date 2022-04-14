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
    }

    [HttpGet(Name = "GetInfo")]
    public IEnumerable<OriginalFile> Get()
    {
        return this.ofs = db.OriginalFiles.Include(o => o.convertedFiles).Include(o => o.conversions).ToList();
    }

    [HttpDelete(Name = "DeleteConvertedFiles")]
    public void DeleteConvertedFiles()
    {
        var rows = from o in db.ConvertedFiles select o;
        foreach (var row in rows)
        {
            db.ConvertedFiles.Remove(row);
        }
        db.SaveChanges();
    }

    [HttpPost(Name = "PostProcessImages")]
    public void ProcessImages()
    {
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
        var conversionsList = new List<ConversionInfo>(Util.DEFAULT_CONVERSIONS).Select(ci => ci.copy(name)).ToList();
        var originalFile = ofs.SingleOrDefault(c => c.FileName == name) ?? new OriginalFile
        {
            FileName = name,
            Artikelnummer = num,
            FileType = type,
            FileLength = file.Length,
            FileCrc = crc,
            conversions = conversionsList,
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
        var dir = Path.GetDirectoryName(outfile.PhysicalPath);
        if (dir != null) Directory.CreateDirectory(dir);

        // Read first frame of gif image
        using (var image = new MagickImage(srcFilePath))
        using (FileStream fs = System.IO.File.Create(outfile.PhysicalPath))
        using (MagickImageCollection images = new MagickImageCollection())
        {
            Enum.TryParse<MagickFormat>(con.FileType, true, out MagickFormat format);

            image.Strip();
            image.Quality = 100;
            if (con.Width > 0 && con.Height > 0) image.Resize(con.Width, con.Height);

            var shadow = new MagickImage(image.Clone());
            shadow.Quality = 100;
            shadow.Shadow(0, 0, 10, (Percentage)90, MagickColors.Black);   // -background black -shadow 100x10+0+0
            shadow.BackgroundColor = new MagickColor(con.BackgroundColor); // -background from db
                                                                           // +swap changes the order of the images we just add them in a different order
            // -alpha set
            image.Alpha(AlphaOption.Set);
            // -virtual-pixel transparent
            image.VirtualPixelMethod = VirtualPixelMethod.Transparent;
            // -channel A means that the next operations should only change the alpha channel
            // - blur reletive the imgage width
            image.Blur(0, 6.0 * image.Width / 2000.0, Channels.Alpha);
            // -level 50%,100%
            image.Level(new Percentage(50), new Percentage(100), Channels.Alpha);
            // +channel cancels only allow operations on the alpha channel.

            images.Add(shadow);
            images.Add(image.Clone());
            using (var merged = images.Merge()) // -layers merge
            {
                // Save
                merged.Write(fs, format);
                fs.Flush();
                outfile = convertedRepo.GetFileInfo(conversionFilePath);
            }
        }
        return outfile;
    }
}
