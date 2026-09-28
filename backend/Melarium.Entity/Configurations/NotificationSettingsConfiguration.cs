using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class NotificationSettingsConfiguration : IEntityTypeConfiguration<NotificationSettings>
{
    public void Configure(EntityTypeBuilder<NotificationSettings> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.UserId).IsUnique();

        builder.HasOne(e => e.User)
            .WithOne()
            .HasForeignKey<NotificationSettings>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("NotificationSettings");
    }
}
