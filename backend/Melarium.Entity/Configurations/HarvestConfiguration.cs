using Melarium.Domain.Entities;
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

        builder.Property(h => h.HoneyType)
            .IsRequired();

        builder.Property(h => h.PricePerKg)
            .HasColumnType("numeric(8,2)");

        builder.Property(h => h.Notes)
            .HasMaxLength(500);

        builder.HasOne(h => h.Apiary)
            .WithMany()
            .HasForeignKey(h => h.ApiaryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.CreatedBy)
            .WithMany()
            .HasForeignKey(h => h.CreatedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasMany(h => h.Entries)
            .WithOne(e => e.Harvest)
            .HasForeignKey(e => e.HarvestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => h.ApiaryId);
        builder.HasIndex(h => h.Date);

        builder.ToTable("Harvests");
    }
}
