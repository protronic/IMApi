using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

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
    public uint Artikelnummer { get; init; }
    public string? FileType { get; set; }
    public uint FileCrc { get; init; }
    public long FileLength { get; init; }
    public Uri? WebURL { get; init; }
    public List<ConvertedFile> convertedFiles { get; } = new();
}

public record ConvertedFile
{
    [Key]
    public string? FileName { get; init; }
    public string? ConversionType { get; init; }
    public uint FileCrc { get; init; }
    public long FileLength { get; init; }
    public Uri? WebURL { get; init; }
}

public class WrongFilenameFormatException : ArgumentException
{
    public WrongFilenameFormatException(string? message) : base(message) { }
}

static public class Util
{

    public static string GenerateConvertedFileName(string name, string conversionName, string fileType = "png")
    {
        return name + "_" + conversionName + "." + fileType;
    }
    public static void GetInfoFromFileName(string originalFileName, out string name, out uint artikelnummer, out string fileType)
    {
        var r = new Regex(@"(\d+)_?(\d+)?.(\w+)", RegexOptions.IgnoreCase);
        var match = r.Match(originalFileName);
        if (match.Success)
        {
            artikelnummer = uint.Parse(match.Groups[1].Value);
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
}
