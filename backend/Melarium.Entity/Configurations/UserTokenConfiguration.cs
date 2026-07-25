using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(t => t.Purpose)
            .IsRequired();

        // Lookups always come in as (hash, purpose) — a token issued for one purpose must never
        // resolve for another.
        builder.HasIndex(t => new { t.TokenHash, t.Purpose }).IsUnique();

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Supports invalidating a user's outstanding tokens of one purpose in a single query.
        builder.HasIndex(t => new { t.UserId, t.Purpose });

        builder.ToTable("UserTokens");
    }
}
