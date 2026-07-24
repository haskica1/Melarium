using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class TreatmentEntryConfiguration : IEntityTypeConfiguration<TreatmentEntry>
{
    public void Configure(EntityTypeBuilder<TreatmentEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DoseNote).HasMaxLength(100);

        // Deleting a hive removes its treatment lines — same documented v1 trade-off as harvests
        // (the printed/archived PDF register is the durable legal record).
        builder.HasOne(e => e.Beehive)
            .WithMany()
            .HasForeignKey(e => e.BeehiveId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.TreatmentId);
        builder.HasIndex(e => e.BeehiveId);

        builder.ToTable("TreatmentEntries");
    }
}
