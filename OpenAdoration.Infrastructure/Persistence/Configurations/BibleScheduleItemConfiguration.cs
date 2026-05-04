using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Infrastructure.Persistence.Configurations;

public sealed class BibleScheduleItemConfiguration : IEntityTypeConfiguration<BibleScheduleItem>
{
    public void Configure(EntityTypeBuilder<BibleScheduleItem> builder)
    {
        builder.Property(b => b.Book)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.Chapter)
            .IsRequired();

        builder.Property(b => b.VerseStart)
            .IsRequired();

        builder.Property(b => b.VerseEnd)
            .IsRequired();

        // Computed — not persisted
        builder.Ignore(b => b.Reference);

        builder.HasOne(b => b.BibleVersion)
            .WithMany()
            .HasForeignKey(b => b.BibleVersionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
