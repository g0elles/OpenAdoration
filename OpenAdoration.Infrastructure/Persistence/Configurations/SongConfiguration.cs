using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Infrastructure.Persistence.Configurations;

public sealed class SongConfiguration : IEntityTypeConfiguration<Song>
{
    public void Configure(EntityTypeBuilder<Song> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Author)
            .HasMaxLength(200);

        builder.Property(s => s.Classification)
            .HasMaxLength(100);

        builder.HasMany(s => s.Sections)
            .WithOne(ss => ss.Song)
            .HasForeignKey(ss => ss.SongId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.SourceGuid)
            .HasMaxLength(64);

        // Content-level theme (M14). No navigation property — Song doesn't need to walk to Theme;
        // deleting a theme nulls the song's reference (mirrors ScheduleItem.ThemeId).
        builder.HasOne<Theme>()
            .WithMany()
            .HasForeignKey(s => s.ThemeId)
            .OnDelete(DeleteBehavior.SetNull);

        // Speed up title search and list ordering
        builder.HasIndex(s => s.Title);

        // Dedup lookup for cross-agenda imports
        builder.HasIndex(s => s.SourceGuid);
    }
}
