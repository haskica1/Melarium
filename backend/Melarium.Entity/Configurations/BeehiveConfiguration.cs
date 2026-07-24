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

        // One beehive → many diets; cascade delete
        builder.HasMany(b => b.Diets)
            .WithOne(d => d.Beehive)
            .HasForeignKey(d => d.BeehiveId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Beehives");
    }
}
