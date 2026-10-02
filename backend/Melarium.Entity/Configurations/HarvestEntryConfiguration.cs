using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class HarvestEntryConfiguration : IEntityTypeConfiguration<HarvestEntry>
{
    public void Configure(EntityTypeBuilder<HarvestEntry> builder)
    {
        builder.HasKey(e => e.Id);

        // Widened from numeric(6,2) in SPEC-30 (approved exception, see ignore.md): grams of royal
        // jelly and milligrams of venom do not fit in two decimals of a kilogram.
        builder.Property(e => e.QuantityKg)
            .HasColumnType("numeric(12,6)");

        // A harvest is deleted with its entries (cascade from Harvest). The hive FK also cascades:
        // deleting a beehive removes its harvest lines — documented v1 trade-off (totals recompute).
        builder.HasOne(e => e.Beehive)
            .WithMany()
            .HasForeignKey(e => e.BeehiveId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.HarvestId);
        builder.HasIndex(e => e.BeehiveId);

        builder.ToTable("HarvestEntries");
    }
}
