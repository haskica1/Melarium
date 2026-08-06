using Melarium.Application.Common.Validation;
using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.Phone)
            .HasMaxLength(PhoneRules.MaxLength);

        // Filtered so the many pre-existing accounts without a number don't collide on NULL —
        // same shape as Beehive.UniqueId.
        builder.HasIndex(u => u.Phone)
            .IsUnique()
            .HasFilter("\"Phone\" IS NOT NULL");

        builder.Property(u => u.ReferralCode)
            .HasMaxLength(64);

        // Filtered for the same reason as Phone: only users who have opened /invite have one,
        // and NULLs must not collide.
        builder.HasIndex(u => u.ReferralCode)
            .IsUnique()
            .HasFilter("\"ReferralCode\" IS NOT NULL");

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Role)
            .IsRequired();

        // Admin-level users are scoped to a single apiary
        builder.HasOne(u => u.Apiary)
            .WithMany()
            .HasForeignKey(u => u.ApiaryId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.ToTable("Users");
    }
}
