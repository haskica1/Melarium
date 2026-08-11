using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class TreatmentConfiguration : IEntityTypeConfiguration<Treatment>
{
    public void Configure(EntityTypeBuilder<Treatment> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Purpose).IsRequired();
        builder.Property(t => t.ActiveSubstance).IsRequired();
        builder.Property(t => t.Method).IsRequired();
        builder.Property(t => t.StartDate).IsRequired();
        builder.Property(t => t.WithdrawalDays).IsRequired();

        builder.Property(t => t.ProductName).IsRequired().HasMaxLength(100);
        builder.Property(t => t.DosePerHive).IsRequired().HasMaxLength(100);
        builder.Property(t => t.BatchNumber).HasMaxLength(50);
        builder.Property(t => t.Supplier).HasMaxLength(100);
        builder.Property(t => t.Notes).HasMaxLength(500);

        builder.HasOne(t => t.Apiary)
            .WithMany()
            .HasForeignKey(t => t.ApiaryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasMany(t => t.Entries)
            .WithOne(e => e.Treatment)
            .HasForeignKey(e => e.TreatmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Rounds)
            .WithOne(r => r.Treatment)
            .HasForeignKey(r => r.TreatmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.ApiaryId);
        builder.HasIndex(t => t.StartDate);

        builder.ToTable("Treatments");
    }
}
