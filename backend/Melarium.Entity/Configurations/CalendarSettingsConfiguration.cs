using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class CalendarSettingsConfiguration : IEntityTypeConfiguration<CalendarSettings>
{
    public void Configure(EntityTypeBuilder<CalendarSettings> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FeedToken).HasMaxLength(128);

        builder.HasIndex(e => e.UserId).IsUnique();
        // Postgres treats NULLs as distinct, so many users may have no token yet under a unique index.
        builder.HasIndex(e => e.FeedToken).IsUnique();

        builder.HasOne(e => e.User)
            .WithOne()
            .HasForeignKey<CalendarSettings>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("CalendarSettings");
    }
}
