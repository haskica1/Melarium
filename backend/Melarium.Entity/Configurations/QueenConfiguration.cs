using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class QueenConfiguration : IEntityTypeConfiguration<Queen>
{
    public void Configure(EntityTypeBuilder<Queen> builder)
    {
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Year)
            .IsRequired();

        builder.Property(q => q.MarkColor)
            .IsRequired();

        builder.Property(q => q.Origin)
            .IsRequired();

        builder.Property(q => q.Status)
            .IsRequired();

        builder.Property(q => q.IntroducedDate)
            .IsRequired();

        builder.Property(q => q.Notes)
            .HasMaxLength(500);

        builder.HasOne(q => q.Beehive)
            .WithMany(b => b.Queens)
            .HasForeignKey(q => q.BeehiveId)
            .OnDelete(DeleteBehavior.Cascade);

        // Named HasIndex overloads — two distinct indexes on the same column
        // (an unnamed second HasIndex would silently replace the first in the model).
        builder.HasIndex(q => q.BeehiveId, "IX_Queens_BeehiveId");

        // DB-enforced invariant: at most one Active queen per beehive.
        // Partial unique index over rows where Status = Active (1).
        builder.HasIndex(q => q.BeehiveId, "IX_Queens_BeehiveId_ActiveUnique")
            .HasFilter("\"Status\" = 1")
            .IsUnique();

        builder.ToTable("Queens");
    }
}
