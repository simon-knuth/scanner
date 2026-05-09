using Microsoft.EntityFrameworkCore;

namespace Scanner.Data;

internal class KnownScannersDbContext : DbContext
{
    private readonly string _databasePath;

    public KnownScannersDbContext(string databasePath)
    {
        _databasePath = databasePath;
    }

    public DbSet<KnownScannerEntry> KnownScannerEntries => Set<KnownScannerEntry>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={_databasePath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KnownScannerEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired();
            entity.Property(e => e.Name).IsRequired();
        });
    }
}