using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Infrastructure.Persistence.Configurations;

public sealed class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.HasKey(mf => mf.Id);

        builder.Property(mf => mf.FileName)
            .IsRequired()
            .HasMaxLength(260); // MAX_PATH on Windows

        builder.Property(mf => mf.FilePath)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(mf => mf.Type)
            .IsRequired()
            .HasConversion<string>();
    }
}
