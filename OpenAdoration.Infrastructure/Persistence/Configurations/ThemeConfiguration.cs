using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Infrastructure.Persistence.Configurations;

public sealed class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    // Fixed timestamps for seed data — must not use DateTime.UtcNow (changes on every migration)
    private static readonly DateTime SeedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.FontFamily)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.FontSize)
            .IsRequired();

        builder.Property(t => t.FontColor)
            .IsRequired()
            .HasMaxLength(9); // #AARRGGBB

        builder.Property(t => t.BackgroundColor)
            .IsRequired()
            .HasMaxLength(9);

        builder.Property(t => t.BackgroundImagePath)
            .HasMaxLength(1024);

        // Seed the default theme so the app works on first launch without configuration
        builder.HasData(new Theme
        {
            Id = 1,
            Name = "Default",
            FontFamily = "Arial",
            FontSize = 48,
            FontColor = "#FFFFFF",
            BackgroundColor = "#000000",
            BackgroundImagePath = null,
            IsDefault = true,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate
        });
    }
}
