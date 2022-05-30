using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using DamienG.Security.Cryptography;

namespace Protronic.CeckedFileInfo;

public class CheckedFileContext : DbContext
{
    public DbSet<OriginalFile> OriginalFiles { get; set; }
    public DbSet<ConvertedFile> ConvertedFiles { get; set; }
    public DbSet<ConversionInfo> Conversions { get; set; }


    public string DbPath { get; }

    public CheckedFileContext()
    {
        // var folder = Environment.SpecialFolder.LocalApplicationData;        
        DbPath = System.IO.Path.Join("./wwwroot/", "CeckedFileInfo.db");
        _ = this.OriginalFiles ?? throw new NullReferenceException(nameof(OriginalFiles));
        _ = this.ConvertedFiles ?? throw new NullReferenceException(nameof(ConvertedFiles));
        _ = this.Conversions ?? throw new NullReferenceException(nameof(Conversions));
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}

public enum ConversionType
{
    web, thumb, _100x100, _200x200, _2000x2000, max
}

public enum Lang
{
    DE, EN
}


public record FileMeta
{
    [Key]
    public Uri WebURL { get; set; } = null!;
    public string FileName { get; init; } = null!;
    public string Artikelnummer { get; init; } = null!;
    public Lang lang { get; init; } = Lang.DE;
    public string FileType { get; set; } = null!;
    public uint FileCrc { get; set; }
    public long FileLength { get; set; }
}

public record OriginalFile
{
    [Key]
    public string FilePath { get; init; } = null!;
    public FileMeta FileMetaData { get; set; } = null!;
    public List<ConversionInfo> Conversions { get; } = new();
    public List<ConvertedFile> ConvertedFiles { get; } = new();
}

public record ConversionInfo
{
    [Key]
    public string ConveretedFilePath { get; set; } = null!;
    public string ConversionName { get; init; } = null!;
    public string FileType { get; init; } = "png";
    public ConversionType Type { get; init; }
    public string? Label { get; set; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string BackgroundColor { get; init; } = "#00000000"; // MagickColor.Transparent

    public ConversionInfo getInstance(string newName)
    {
        var cp = (ConversionInfo)this.MemberwiseClone();
        cp.ConveretedFilePath = cp.ConversionName + "/" + newName + (String.IsNullOrEmpty(this.Label) ? ("_" + this.Label) : "") + "." + cp.FileType;
        return cp;
    }

}

public record ConvertedFile
{
    [Key]
    public string ConveretedFilePath { get; init; } = null!;
    public FileMeta FileMetaData { get; set; } = null!;
    public ConversionInfo Conversion { get; init; } = null!;
}

public class WrongFilenameFormatException : ArgumentException
{
    public WrongFilenameFormatException(string? message) : base(message) { }
}

static public class Util
{
    public static ConversionInfo[] DEFAULT_CONVERSIONS = {
        new ConversionInfo {
            ConversionName = "web",
            Type = ConversionType.web,
            Label = string.Empty,
            Width = 500,
            Height = 500,
            BackgroundColor = "#FFFFFFFF",
        },
        new ConversionInfo {
            ConversionName = "thumb",
            Type = ConversionType.thumb,
            Label = string.Empty,
            Width = 100,
            Height = 100,
            BackgroundColor = "#FFFFFFFF",
        },
        new ConversionInfo {
            ConversionName = "100x100",
            Type = ConversionType._100x100,
            Label = string.Empty,
            Width = 100,
            Height = 100
        },
        new ConversionInfo {
            ConversionName = "200x200",
            Type = ConversionType._200x200,
            Label = string.Empty,
            Width = 200,
            Height = 200
        },
        new ConversionInfo {
            ConversionName = "2000x2000",
            Type = ConversionType._2000x2000,
            Label = string.Empty,
            Width = 2000,
            Height = 2000
        },
        new ConversionInfo {
            ConversionName = "max",
            Type = ConversionType.max,
            Label = string.Empty,
            Width = -1,
            Height = -1
        }
    };

    // private static ConversionInfo changeLabel(ConversionInfo ci, string label){
    //     ConversionInfo result = new ConversionInfo();
    //     result.
    //     // ci.Label = label;
    //     return ci;
    // }

    public static ConversionInfo[] getLabeledConversionInfo(string newLabel)
    {
        return DEFAULT_CONVERSIONS.Select(v => new ConversionInfo
        {
            ConversionName = v.ConversionName,
            Type = v.Type,
            Label = newLabel,
            Width = v.Width,
            Height = v.Height
        }).ToArray();
    }

    public static void GetInfoFromFileName(string originalFileName, out string name, out string artikelnummer, out Lang lang, out string fileType)
    {
        var r = new Regex(@"((?'num'\d+)(_(?'index'[\d]+)|_(?'lang'[a-zA-Z]+))*(\.(?i)(?'ext'jpg|png|gif|bmp))$)", RegexOptions.IgnoreCase);
        var match = r.Match(originalFileName);
        if (match.Success)
        {
            artikelnummer = match.Groups["num"].Value;
            lang = match.Groups["lang"].Success ? Enum.Parse<Lang>(match.Groups["lang"].Value.ToUpper()) : Lang.DE;
            name = match.Groups["num"].Value;
            name += match.Groups["lang"].Success ? "_" + match.Groups["lang"].Value : string.Empty;
            name += match.Groups["index"].Success ? "_" + match.Groups["index"].Value : string.Empty;
            fileType = match.Groups["ext"].Value;
        }
        else
            throw new WrongFilenameFormatException(originalFileName);
    }

    public static void AddOrUpdate(this DbContext ctx, object entity, ILogger logger)
    {
        var entry = ctx.Entry(entity);
        switch (entry.State)
        {
            case EntityState.Detached:
                ctx.Add(entity);
                break;
            case EntityState.Modified:
                logger.LogInformation("Orginal File has changed, drop converted files");
                (entity as OriginalFile)?.ConvertedFiles.Clear();
                ctx.Update(entity);
                break;
            case EntityState.Added:
                ctx.Add(entity);
                break;
            case EntityState.Unchanged:
                //item already in db no need to do anything  
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
        logger.LogInformation($"EntityState: {entry.State}");
    }

    public static void InsertOrUpdateRange<T, T2>(this T entity, List<T2> updateEntity, DbContext context, ILogger logger)
        where T : class
        where T2 : class
    {
        foreach (var e in updateEntity)
        {
            context.Set<T2>().InsertOrUpdate(e, context, logger);
        }
    }


    public static void InsertOrUpdate<T, T2>(this T entity, T2 updateEntity, DbContext context,  ILogger logger)
    where T : class
    where T2 : class
    {
        var entry = context.Entry(updateEntity);
        if (entry.State == EntityState.Detached)
        {
            if (context.Set<T2>().Any(t => t == updateEntity))
            {
                context.Set<T2>().Update(updateEntity);                
            }
            else
            {
                context.Set<T2>().Add(updateEntity);
            }

        }
        logger.LogInformation($"EntityState: {entry.State}");
    }

    public static void checkFile(IFileInfo file, ILogger logger, out string filename,
        out string artikelnummer, out Lang language, out string filetype, out uint crc)
    {
        Crc32 crc32 = new Crc32();
        String hash = String.Empty;
        using (Stream fs = file.CreateReadStream())
        {
            var bytes = crc32.ComputeHash(fs);
            foreach (byte b in bytes) hash += b.ToString("x2").ToLower();

            logger.LogInformation("CRC-32 is {0}", hash);
            Util.GetInfoFromFileName(Path.GetFileName(file.PhysicalPath), out string name, out string num, out Lang lang, out string type);
            filename = name;
            artikelnummer = num;
            filetype = type;
            language = lang;
            crc = Crc32.getUIntResult(bytes);
        }
    }
    public static string getFileName(OriginalFile f)
    {
        return f.FileMetaData.FileName + "." + f.FileMetaData.FileType;
    }

    public static string getFileName(ConvertedFile f)
    {
        return Path.Combine(f.Conversion.ConversionName, Path.GetFileNameWithoutExtension(f.FileMetaData.FileName)) + "." + f.FileMetaData.FileType;
    }
}
