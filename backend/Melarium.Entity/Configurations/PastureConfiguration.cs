using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class PastureConfiguration : IEntityTypeConfiguration<Pasture>
{
    public void Configure(EntityTypeBuilder<Pasture> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Address).HasMaxLength(200);
        builder.Property(p => p.FloraNotes).HasMaxLength(300);
        builder.Property(p => p.Notes).HasMaxLength(500);

        builder.HasOne(p => p.Organization)
            .WithMany()
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.OrganizationId);

        builder.ToTable("Pastures");
    }
}
