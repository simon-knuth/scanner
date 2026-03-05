using Microsoft.EntityFrameworkCore;
using Scanner.Models;

namespace Scanner.Data;

internal class ProjectHistoryDbContext : DbContext
{
    private readonly string _databasePath;

    public ProjectHistoryDbContext(string databasePath)
    {
        _databasePath = databasePath;
    }

    public DbSet<ProjectHistoryEntry> ProjectHistoryEntries => Set<ProjectHistoryEntry>();
    public DbSet<ProjectHistoryFile> ProjectHistoryFiles => Set<ProjectHistoryFile>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={_databasePath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectHistoryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Format).IsRequired();
            entity.Property(e => e.LastUsed).IsRequired();
            entity.HasMany(e => e.Files)
                  .WithOne(f => f.ProjectHistoryEntry)
                  .HasForeignKey(f => f.ProjectHistoryEntryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectHistoryFile>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.FilePath).IsRequired();
        });
    }
}