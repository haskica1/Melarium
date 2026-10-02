using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class HarvestConfiguration : IEntityTypeConfiguration<Harvest>
{
    public void Configure(EntityTypeBuilder<Harvest> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Date)
            .IsRequired();

        // SPEC-30 (approved exception to the frozen-configuration rule, see ignore.md): HoneyType is
        // honey-only now, so it is optional; the validator still requires it for honey.
        builder.Property(h => h.HoneyType);

        // Every row recorded before SPEC-30 is honey — the default is what the migration gives them.
        builder.Property(h => h.ProductType)
            .IsRequired()
            .HasDefaultValue(HiveProductType.Honey);

        builder.Property(h => h.PricePerKg)
            .HasColumnType("numeric(8,2)");

        // Six decimals of a kg is one milligram — the resolution bee venom is collected in.
        builder.Property(h => h.BulkKg)
            .HasColumnType("numeric(12,6)");

        builder.Property(h => h.Notes)
            .HasMaxLength(500);

        builder.HasOne(h => h.Organization)
            .WithMany()
            .HasForeignKey(h => h.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional since SPEC-30: no apiary = a record of the whole organization. Still Cascade —
        // SetNull would silently turn a deleted apiary's harvests into shared ones.
        builder.HasOne(h => h.Apiary)
            .WithMany()
            .HasForeignKey(h => h.ApiaryId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne(h => h.CreatedBy)
            .WithMany()
            .HasForeignKey(h => h.CreatedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasMany(h => h.Entries)
            .WithOne(e => e.Harvest)
            .HasForeignKey(e => e.HarvestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => h.OrganizationId);
        builder.HasIndex(h => h.ApiaryId);
        builder.HasIndex(h => h.Date);

        builder.ToTable("Harvests");
    }
}
