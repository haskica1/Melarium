using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class FeedingEntryConfiguration : IEntityTypeConfiguration<FeedingEntry>
{
    public void Configure(EntityTypeBuilder<FeedingEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ScheduledDate).IsRequired();
        builder.Property(e => e.Status).IsRequired();

        builder.HasIndex(e => e.DietId);
        builder.HasIndex(e => e.ScheduledDate);

        builder.ToTable("FeedingEntries");
    }
}
