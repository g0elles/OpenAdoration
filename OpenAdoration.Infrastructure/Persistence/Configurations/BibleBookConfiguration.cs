using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Infrastructure.Persistence.Configurations;

public sealed class BibleBookConfiguration : IEntityTypeConfiguration<BibleBook>
{
    public void Configure(EntityTypeBuilder<BibleBook> builder)
    {
        builder.HasKey(bb => bb.Id);

        builder.Property(bb => bb.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(bb => bb.Abbreviation)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(bb => bb.Testament)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(bb => bb.BookNumber)
            .IsRequired();

        builder.Property(bb => bb.ChapterCount)
            .IsRequired();

        // Primary lookup path: version → books in canonical order
        builder.HasIndex(bb => new { bb.BibleVersionId, bb.BookNumber });
    }
}
