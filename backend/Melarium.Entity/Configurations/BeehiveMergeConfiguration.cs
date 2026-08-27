using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class BeehiveMergeConfiguration : IEntityTypeConfiguration<BeehiveMerge>
{
    public void Configure(EntityTypeBuilder<BeehiveMerge> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.MergedAt).IsRequired();
        builder.Property(m => m.Notes).HasMaxLength(1000);

        // Both ends Restrict (SPEC-19 §2): a cascade from either hive would silently delete the
        // record of the merge — and with it the undo journal, which is the only copy of the todos
        // the merge removed.
        builder.HasOne(m => m.SourceBeehive)
            .WithMany()
            .HasForeignKey(m => m.SourceBeehiveId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.TargetBeehive)
            .WithMany()
            .HasForeignKey(m => m.TargetBeehiveId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.CreatedBy)
            .WithMany()
            .HasForeignKey(m => m.CreatedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(m => m.UndoneBy)
            .WithMany()
            .HasForeignKey(m => m.UndoneById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // A hive can be the source of at most one merge in force; undone rows are excluded so the
        // same hive can be merged again after a correction.
        builder.HasIndex(m => m.SourceBeehiveId)
            .IsUnique()
            .HasFilter("\"UndoneAt\" IS NULL");

        builder.HasIndex(m => m.TargetBeehiveId);
        builder.HasIndex(m => m.MergedAt);

        builder.ToTable("BeehiveMerges");
    }
}
