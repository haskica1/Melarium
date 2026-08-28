using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).IsRequired().HasMaxLength(150);
        builder.Property(a => a.BodyMarkdown).IsRequired();
        builder.Property(a => a.Type).IsRequired();

        builder.HasMany(a => a.Reads)
            .WithOne(r => r.Announcement)
            .HasForeignKey(r => r.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.IsPublished);
        builder.HasIndex(a => a.PublishedAt);

        builder.ToTable("Announcements");
    }
}
