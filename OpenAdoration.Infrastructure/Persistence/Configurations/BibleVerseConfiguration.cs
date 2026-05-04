using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Infrastructure.Persistence.Configurations;

public sealed class BibleVerseConfiguration : IEntityTypeConfiguration<BibleVerse>
{
    public void Configure(EntityTypeBuilder<BibleVerse> builder)
    {
        builder.HasKey(bv => bv.Id);

        builder.Property(bv => bv.Book)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(bv => bv.Chapter)
            .IsRequired();

        builder.Property(bv => bv.Verse)
            .IsRequired();

        builder.Property(bv => bv.Text)
            .IsRequired();

        // Computed — not persisted
        builder.Ignore(bv => bv.Reference);

        // Primary lookup: version + book + chapter → all verses in a chapter
        builder.HasIndex(bv => new { bv.BibleVersionId, bv.Book, bv.Chapter });

        // Unique constraint: no duplicate verses per version
        builder.HasIndex(bv => new { bv.BibleVersionId, bv.Book, bv.Chapter, bv.Verse })
            .IsUnique();

        builder.HasOne(bv => bv.BibleVersion)
            .WithMany()
            .HasForeignKey(bv => bv.BibleVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
