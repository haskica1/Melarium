using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class ApiaryConfiguration : IEntityTypeConfiguration<Apiary>
{
    public void Configure(EntityTypeBuilder<Apiary> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Description)
            .HasMaxLength(1000);

        builder.Property(a => a.Latitude)
            .HasColumnType("double precision");

        builder.Property(a => a.Longitude)
            .HasColumnType("double precision");

        builder.Property(a => a.HomeLatitude)
            .HasColumnType("double precision");

        builder.Property(a => a.HomeLongitude)
            .HasColumnType("double precision");

        builder.HasOne(a => a.CreatedBy)
            .WithMany()
            .HasForeignKey(a => a.CreatedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Current pasture (SPEC-10); service-level guard blocks deleting a referenced pasture.
        builder.HasOne(a => a.CurrentPasture)
            .WithMany()
            .HasForeignKey(a => a.CurrentPastureId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // One apiary → many beehives; cascade delete removes beehives when apiary is deleted
        builder.HasMany(a => a.Beehives)
            .WithOne(b => b.Apiary)
            .HasForeignKey(b => b.ApiaryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Apiaries");
    }
}
