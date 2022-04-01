using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Protronic.CeckedFileInfo;
public class CheckedFileContext : DbContext
{
    public DbSet<CheckedFile>? Files { get; set; }
    
    public string DbPath { get; }

    public CheckedFileContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = System.IO.Path.Join(path, "CeckedFileInfo.db");
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}

public record CheckedFile
{
    [Key]
    public UInt32 FileCrcId { get; init; }
    [Column(TypeName = "jsonb")]
    public IFileInfo? FileInfo { get; init; }
}
