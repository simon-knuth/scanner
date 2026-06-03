using Microsoft.EntityFrameworkCore;
using Scanner.Models;

namespace Scanner.Data;

internal class TemplatesDbContext : DbContext
{
    private const int BusyTimeoutSeconds = 5;

    private readonly string _databasePath;

    public TemplatesDbContext(string databasePath)
    {
        _databasePath = databasePath;
    }

    public DbSet<TemplateEntry> TemplateEntries => Set<TemplateEntry>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite(
            $"Data Source={_databasePath};Pooling=False",
            sqlite => sqlite.CommandTimeout(BusyTimeoutSeconds));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TemplateEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Created).IsRequired();
            entity.Property(e => e.LastUsed).IsRequired();
        });
    }
}
