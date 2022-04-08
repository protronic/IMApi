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

    public string DbPath { get; }

    public CheckedFileContext()
    {
        // var folder = Environment.SpecialFolder.LocalApplicationData;        
        DbPath = System.IO.Path.Join("./wwwroot/", "CeckedFileInfo.db");
        _ = this.OriginalFiles ?? throw new NullReferenceException(nameof(OriginalFiles));
        _ = this.ConvertedFiles ?? throw new NullReferenceException(nameof(ConvertedFiles));
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}

public record OriginalFile
{
    [Key]
    public string? FileName { get; init; }
    public string? Artikelnummer { get; init; }
    public string? FileType { get; set; }
    public uint FileCrc { get; init; }
    public long FileLength { get; init; }
    public Uri? WebURL { get; init; }
    public List<ConvertedFile> convertedFiles { get; } = new();
}

public record ConvertedFile
{
    [Key]
    public Uri? WebURL { get; init; }
    public string? FileName { get; init; }
    public string? ConversionType { get; init; }
    public string? FileType { get; set; }
    public uint FileCrc { get; init; }
    public long FileLength { get; init; }
}

public class WrongFilenameFormatException : ArgumentException
{
    public WrongFilenameFormatException(string? message) : base(message) { }
}

static public class Util
{
    public static void GetInfoFromFileName(string originalFileName, out string name, out string artikelnummer, out string fileType)
    {
        var r = new Regex(@"(\d+)_?(\d+)?.(\w+)", RegexOptions.IgnoreCase);
        var match = r.Match(originalFileName);
        if (match.Success)
        {
            artikelnummer = match.Groups[1].Value;
            name = match.Groups[1].Value + match.Groups[2].Value;
            fileType = match.Groups[3].Value;
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
                (entity as OriginalFile)?.convertedFiles.Clear();
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
    }

    public static void checkFile(IFileInfo file, ILogger logger, out string filename, out string artikelnummer, out string filetype, out uint crc)
    {
        Crc32 crc32 = new Crc32();
        String hash = String.Empty;
        using (Stream fs = file.CreateReadStream())
        {
            var bytes = crc32.ComputeHash(fs);
            foreach (byte b in bytes) hash += b.ToString("x2").ToLower();

            logger.LogInformation("CRC-32 is {0}", hash);
            Util.GetInfoFromFileName(Path.GetFileName(file.PhysicalPath), out string name, out string num, out string type);
            filename = name;
            artikelnummer =num;
            filetype = type;
            crc = Crc32.getUIntResult(bytes);
        }
    }
    public static string getFileName(OriginalFile f){
        return f.FileName + "." + f.FileType;
    }
}
