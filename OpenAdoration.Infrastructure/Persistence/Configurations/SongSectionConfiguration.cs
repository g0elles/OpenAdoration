using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Infrastructure.Persistence.Configurations;

public sealed class SongSectionConfiguration : IEntityTypeConfiguration<SongSection>
{
    public void Configure(EntityTypeBuilder<SongSection> builder)
    {
        builder.HasKey(ss => ss.Id);

        builder.Property(ss => ss.Type)
            .IsRequired()
            .HasConversion<string>(); // Stored as "Verse", "Chorus", etc. — readable in DB

        builder.Property(ss => ss.SectionNumber)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(ss => ss.Lyrics)
            .IsRequired();

        builder.Property(ss => ss.Order)
            .IsRequired();

        // Label is a computed property — not persisted
        builder.Ignore(ss => ss.Label);

        builder.HasIndex(ss => new { ss.SongId, ss.Order });
    }
}
