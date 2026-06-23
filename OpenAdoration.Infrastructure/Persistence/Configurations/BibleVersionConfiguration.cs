using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Infrastructure.Persistence.Configurations;

public sealed class BibleVersionConfiguration : IEntityTypeConfiguration<BibleVersion>
{
    public void Configure(EntityTypeBuilder<BibleVersion> builder)
    {
        builder.HasKey(bv => bv.Id);

        builder.Property(bv => bv.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(bv => bv.Abbreviation)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(bv => bv.Language)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(bv => bv.SourcePluginId)
            .HasMaxLength(100);

        builder.HasMany(bv => bv.Books)
            .WithOne(bb => bb.BibleVersion)
            .HasForeignKey(bb => bb.BibleVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Abbreviation must be unique per installation
        builder.HasIndex(bv => bv.Abbreviation).IsUnique();
    }
}
