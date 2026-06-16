using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Infrastructure.Persistence.Configurations;

public sealed class WorshipServiceConfiguration : IEntityTypeConfiguration<WorshipService>
{
    public void Configure(EntityTypeBuilder<WorshipService> builder)
    {
        builder.HasKey(ws => ws.Id);

        builder.Property(ws => ws.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(ws => ws.Date)
            .IsRequired();

        builder.HasMany(ws => ws.Items)
            .WithOne(si => si.Service)
            .HasForeignKey(si => si.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(ws => ws.SourceGuid)
            .HasMaxLength(64);

        builder.Property(ws => ws.SourceArchivePath)
            .HasMaxLength(1024);

        builder.HasIndex(ws => ws.Date);

        // Dedup lookup so re-importing the same agenda is detected
        builder.HasIndex(ws => ws.SourceGuid);
    }
}
