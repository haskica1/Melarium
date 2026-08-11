using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class TreatmentRoundConfiguration : IEntityTypeConfiguration<TreatmentRound>
{
    public void Configure(EntityTypeBuilder<TreatmentRound> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ScheduledDate).IsRequired();
        builder.Property(r => r.Status).IsRequired();
        builder.Property(r => r.Note).HasMaxLength(300);

        builder.HasIndex(r => r.TreatmentId);
        builder.HasIndex(r => r.ScheduledDate);

        builder.ToTable("TreatmentRounds");
    }
}
