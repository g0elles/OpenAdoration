using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Infrastructure.Persistence.Configurations;

public sealed class ScheduleItemConfiguration : IEntityTypeConfiguration<ScheduleItem>
{
    public void Configure(EntityTypeBuilder<ScheduleItem> builder)
    {
        builder.HasKey(si => si.Id);

        // TPH: all subtypes share one table, discriminated by "ItemType" column
        builder.HasDiscriminator<string>("ItemType")
            .HasValue<SongScheduleItem>("Song")
            .HasValue<BibleScheduleItem>("Bible")
            .HasValue<MediaScheduleItem>("Media");

        builder.Property(si => si.Order)
            .IsRequired();

        builder.HasOne(si => si.Theme)
            .WithMany()
            .HasForeignKey(si => si.ThemeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(si => new { si.ServiceId, si.Order });
    }
}
