using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

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

    public string GenerateConvertedFileName(string originalFileName, string conversionName, string fileType = "png")
    {
        return originalFileName + "_" + conversionName + "." + fileType;
    }
}

public record OriginalFile
{
    [Key]
    public uint Artikelnummer { get; init; }
    public string? FileName { get; init; }
    public uint FileCrc { get; init; }
    public long FileLength { get; init; }
    public List<ConvertedFile> convertedFiles { get; } = new();
}

public record ConvertedFile
{
    [Key]
    public string? FileName { get; init; }
    public uint FileCrc { get; init; }
    public long FileLength { get; init; }
}
