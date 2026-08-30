using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.Description)
            .HasMaxLength(1000);

        // ── Subscription plan (SPEC-09) — existing rows default to Free ──
        builder.Property(o => o.Plan)
            .IsRequired()
            .HasDefaultValue(Melarium.Domain.Enums.PlanType.Free);

        builder.Property(o => o.PlanNotes)
            .HasMaxLength(300);

        // ── Logo (SPEC-22) ── storage key + sniffed content type, both null until one is uploaded
        builder.Property(o => o.LogoStoragePath)
            .HasMaxLength(500);

        builder.Property(o => o.LogoContentType)
            .HasMaxLength(100);

        builder.HasOne(o => o.CreatedBy)
            .WithMany()
            .HasForeignKey(o => o.CreatedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasMany(o => o.Users)
            .WithOne(u => u.Organization)
            .HasForeignKey(u => u.OrganizationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Apiaries)
            .WithOne(a => a.Organization)
            .HasForeignKey(a => a.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Organizations");
    }
}
