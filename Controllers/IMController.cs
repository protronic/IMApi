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

        this.ofs = db.OriginalFiles
        .Include(o => o.FileMetaData)
        .Include(o => o.ConvertedFiles).ThenInclude(cf => cf.FileMetaData)
        .Include(o => o.Conversions).ToList();
    }

    [HttpGet(Name = "GetInfo")]
    public IEnumerable<OriginalFile> Get()
    {
        return this.ofs;
    }

    [HttpGet("images", Name = "GetImages")]
    public IEnumerable<FileMeta> GetImages()
    {
        return db.OriginalFiles.Select(o => o.FileMetaData);
    }

    [HttpGet("imageNames", Name = "GetImageNames")]
    public IEnumerable<string> GetImageNames()
    {
        return this.originalRepo.GetDirectoryContents("").Select(o => o.Name);
    }

    [HttpGet("images/{itemId}", Name = "GetImageNamesByItem")]
    public IEnumerable<FileMeta> GetItemImages(string itemId)
    {
        return db.OriginalFiles.Where(o => o.FileMetaData.Artikelnummer == itemId).Select(o => o.FileMetaData);
    }

    [HttpDelete(Name = "DeleteConvertedFiles")]
    public void DeleteConvertedFiles()
    {

        var del = db.ConvertedFiles.Include(cf => cf.FileMetaData);
        db.FileMeta.RemoveRange(del.Select(cf => cf.FileMetaData));
        db.ConvertedFiles.RemoveRange(del);
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

    [HttpPost("{imageName}", Name = "PostProcessLabeledImages")]
    public void ProcessImages(string imageName, string? label, string? conversionName)
    {
        // context.HttpContext.Request.Body;  
        // HttpContext.Request.Body;
        foreach (var f in this.originalRepo.GetDirectoryContents("").Where(f => f.Name == imageName))
        {
            logger.LogInformation($"FileName: {f.Name} | Label: {label} | ConversionName: {conversionName}");
            if (GetFormatInformation(f) != null)
            {
                Util.checkFile(f, logger, out string name, out string num, out Lang lang, out string type, out uint crc);
                var originalFile = ofs.SingleOrDefault(c => c.FileMetaData.FileName == name);
                if (originalFile != null)
                {
                    var removeFiles = false;
                    originalFile.Conversions.ForEach(c =>
                    {
                        if (String.IsNullOrEmpty(conversionName) || c.ConversionName == conversionName)
                        {
                            if (c.Label != label)
                                removeFiles = true;
                            c.Label = label;
                        }
                    });
                    if (removeFiles)
                        removeConvertedFileInfo(originalFile, "label has changed", conversionName);
                }
                else
                {
                    originalFile = checkFileHasChanged(f, label, conversionName);
                }
                processConverts(originalFile);
            }
        };
        db.SaveChanges();
    }

    private void removeConvertedFileInfo(OriginalFile o, string reason, string? conversionName = null)
    {
        logger.LogInformation("drop ConvertedFiles for " + o.FileMetaData.FileName + " " + conversionName + " caused by: " + reason);
        var del = db.ConvertedFiles.Include(cf => cf.FileMetaData)
        .Where(cf => cf.FileMetaData.Artikelnummer == o.FileMetaData.Artikelnummer && (String.IsNullOrEmpty(conversionName) || cf.Conversion.ConversionName == conversionName));
        db.FileMeta.RemoveRange(del.Select(cf => cf.FileMetaData));
        db.ConvertedFiles.RemoveRange(del);
        o.ConvertedFiles.Clear();
        db.SaveChanges();
    }

    private IMagickFormatInfo? GetFormatInformation(IFileInfo file)
    {
        return MagickNET.GetFormatInformation(file.PhysicalPath)
            ?? MagickNET.GetFormatInformation(new MagickImageInfo(file.PhysicalPath).Format);
    }

    private void processConverts(OriginalFile originalFile)
    {
        var fileName = Util.getFileName(originalFile);

        foreach (ConversionInfo con in originalFile.Conversions)
        {
            var convertedFile = originalFile.ConvertedFiles.Where(c => con.Equals(c.Conversion)).SingleOrDefault();
            if (convertedFile != null && !convertedRepo.GetFileInfo(Util.getFileName(convertedFile)).Exists)
            {
                originalFile.ConvertedFiles.Remove(convertedFile);
                removeConvertedFileInfo(originalFile, "file not existis", con.ConversionName);
                convertedFile = null;
            }

            if (convertedFile == null)
            {
                var convertedFileInfo = ConvertImageFromOneFormatToAnother(fileName, con);
                Util.checkFile(convertedFileInfo, logger, out string name, out string num, out Lang lang, out string type, out uint crc);
                originalFile.ConvertedFiles.Add(new ConvertedFile
                {
                    ConveretedFilePath = convertedFileInfo.PhysicalPath,
                    FileMetaData = new FileMeta
                    {
                        FileName = name,
                        FileType = type,
                        FileCrc = crc,
                        FileLength = convertedFileInfo.Length,
                        Artikelnummer = num,
                        WebURL = new Uri("/img/out/" + con.ConversionName + "/" + fileName, UriKind.Relative)
                    },
                    Conversion = con,
                });
            }
        }
    }

    private OriginalFile checkFileHasChanged(IFileInfo file, string? label = null, string? conversionName = null)
    {
        Util.checkFile(file, logger, out string name, out string num, out Lang lang, out string type, out uint crc);
        var c = String.IsNullOrEmpty(label) ? Util.DEFAULT_CONVERSIONS : Util.getLabeledConversionInfo(label);
        c = c.Where(con => (String.IsNullOrEmpty(conversionName) || con.ConversionName == conversionName)).ToArray();
        var conversions = new List<ConversionInfo>(c).Select(ci => ci.getInstance(name)).ToList();
        var originalFile = ofs.SingleOrDefault(c => c.FileMetaData.FileName == name) ?? new OriginalFile
        {
            FilePath = file.PhysicalPath,
            FileMetaData = new FileMeta
            {
                FileName = name,
                Artikelnummer = num
            }
        };

        if (originalFile.FileMetaData.FileCrc != crc)
            removeConvertedFileInfo(originalFile, "originalFile has changed");

        originalFile.FileMetaData.FileCrc = crc;
        originalFile.FileMetaData.FileType = type;
        originalFile.FileMetaData.FileLength = file.Length;
        
        originalFile.Conversions.AddRange(
            conversions.Where(x => !originalFile.Conversions.Any(y => y.ConveretedFilePath == x.ConveretedFilePath)));

        originalFile.FileMetaData.WebURL = new Uri("/img/orig/" + Path.GetFileName(file.PhysicalPath), UriKind.Relative);
        Util.AddOrUpdate(db, originalFile, logger);
        return originalFile;
    }

    private IFileInfo ConvertImageFromOneFormatToAnother(string srcfile, ConversionInfo con)
    {
        IFileInfo outfile;
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
            shadow.Shadow(0, 0, 40.0 * image.Width / 2000.0, (Percentage)90, MagickColors.Black);   // -background black -shadow 100x10+0+0
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

            if (!String.IsNullOrEmpty(con.Label))
            {
                var readSettings = new MagickReadSettings
                {
                    Font = "Arial",
                    TextGravity = Gravity.South,
                    FillColor = MagickColors.White,
                    TextUnderColor = new MagickColor("#00000040"),
                    BackgroundColor = MagickColors.Transparent,
                    Height = image.Height, // height of text box
                    Width = image.Width // width of text box
                };

                using (var caption = new MagickImage($"label:{con.Label}", readSettings))
                {
                    //image is your main image where you need to put text
                    image.Composite(caption, Gravity.South, CompositeOperator.Over);
                }
            }

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
