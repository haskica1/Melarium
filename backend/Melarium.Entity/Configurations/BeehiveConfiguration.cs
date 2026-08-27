using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class BeehiveConfiguration : IEntityTypeConfiguration<Beehive>
{
    public void Configure(EntityTypeBuilder<Beehive> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.Notes)
            .HasMaxLength(2000);

        builder.Property(b => b.LabelNumber)
            .HasMaxLength(20);

        builder.Property(b => b.UniqueId)
            .HasColumnType("uuid");

        // Enforce uniqueness on UniqueId (non-null rows only, so nullable GUIDs are still allowed)
        builder.HasIndex(b => b.UniqueId)
            .IsUnique()
            .HasFilter("\"UniqueId\" IS NOT NULL");

        // Store enum as integer for performance; consider string if human-readable DB matters
        builder.Property(b => b.Type)
            .IsRequired();

        builder.Property(b => b.Material)
            .IsRequired();

        builder.HasOne(b => b.CreatedBy)
            .WithMany()
            .HasForeignKey(b => b.CreatedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // One beehive → many inspections; cascade delete
        builder.HasMany(b => b.Inspections)
            .WithOne(i => i.Beehive)
            .HasForeignKey(i => i.BeehiveId)
            .OnDelete(DeleteBehavior.Cascade);

        // Diets are apiary-scoped (SPEC-12) — a hive reaches them through DietBeehive, configured
        // there, so there is no direct Beehive → Diet relationship any more.

        // Colony merge (SPEC-19). Restrict, not cascade: deleting the receiving hive must not take
        // the merged-away hive's rows with it — that hive is the one carrying the retained history.
        builder.HasOne(b => b.MergedIntoBeehive)
            .WithMany()
            .HasForeignKey(b => b.MergedIntoBeehiveId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Every hive list filters on this column (SPEC-19 §5), so it is worth an index.
        builder.HasIndex(b => b.MergedIntoBeehiveId);

        builder.ToTable("Beehives");
    }
}
