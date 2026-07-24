using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class InspectionConfiguration : IEntityTypeConfiguration<Inspection>
{
    public void Configure(EntityTypeBuilder<Inspection> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Date)
            .IsRequired();

        builder.Property(i => i.HoneyLevel)
            .IsRequired();

        builder.Property(i => i.BroodStatus)
            .HasMaxLength(500);

        builder.Property(i => i.Notes)
            .HasMaxLength(2000);

        // Index on Date for common time-range queries
        builder.HasIndex(i => i.Date);
        builder.HasIndex(i => i.BeehiveId);

        builder.ToTable("Inspections");
    }
}
