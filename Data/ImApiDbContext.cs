using Microsoft.EntityFrameworkCore;

namespace IMApi.Data;

/// <summary>Text that is rendered into the images of an article, per language.</summary>
public class ImageText
{
    public string Artikelnummer { get; set; } = null!;
    public string Language { get; set; } = null!;
    public string Text { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class ImApiDbContext(DbContextOptions<ImApiDbContext> options) : DbContext(options)
{
    public DbSet<ImageText> ImageTexts => Set<ImageText>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImageText>(e =>
        {
            e.HasKey(t => new { t.Artikelnummer, t.Language });
            e.Property(t => t.Artikelnummer).HasMaxLength(64);
            e.Property(t => t.Language).HasMaxLength(8);
            e.Property(t => t.Text).HasMaxLength(500);
            e.Property(t => t.UpdatedBy).HasMaxLength(256);
            // SQLite cannot order by DateTimeOffset; store it as UTC ticks.
            e.Property(t => t.UpdatedAt).HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));
        });
    }
}

/// <summary>Lets <c>dotnet ef</c> create the context without starting the web host.</summary>
public class ImApiDbContextFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<ImApiDbContext>
{
    public ImApiDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ImApiDbContext>().UseSqlite("Data Source=data/imapi.db").Options);
}
