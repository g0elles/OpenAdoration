using Microsoft.EntityFrameworkCore;
using OpenAdoration.Domain.Common;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Infrastructure.Persistence.Configurations;

namespace OpenAdoration.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Song> Songs => Set<Song>();
    public DbSet<SongSection> SongSections => Set<SongSection>();
    public DbSet<WorshipService> WorshipServices => Set<WorshipService>();
    public DbSet<ScheduleItem> ScheduleItems => Set<ScheduleItem>();
    public DbSet<BibleVersion> BibleVersions => Set<BibleVersion>();
    public DbSet<BibleBook> BibleBooks => Set<BibleBook>();
    public DbSet<BibleVerse> BibleVerses => Set<BibleVerse>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<Theme> Themes => Set<Theme>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SongConfiguration).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
    }

    private void StampTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    // Prevent accidental overwrites of the original creation date
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
