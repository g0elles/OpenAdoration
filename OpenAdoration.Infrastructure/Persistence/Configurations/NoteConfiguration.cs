using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Infrastructure.Persistence.Configurations;

public sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Content)
            .IsRequired();

        // Content-level theme (M14). No navigation property — mirrors Song.ThemeId.
        builder.HasOne<Theme>()
            .WithMany()
            .HasForeignKey(n => n.ThemeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(n => n.Title);
    }
}
