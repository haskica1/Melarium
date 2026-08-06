using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Source)
            .IsRequired();

        builder.Property(i => i.Status)
            .IsRequired();

        builder.Property(i => i.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(i => i.EmailCanonical)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(i => i.PersonalMessage)
            .HasMaxLength(300);

        // SetNull throughout: deleting a user or an organisation must not delete the ledger that
        // justifies an organisation's extended plan. Losing the attribution is acceptable, losing
        // the record of days we gave away is not.
        builder.HasOne(i => i.Inviter)
            .WithMany()
            .HasForeignKey(i => i.InviterUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.InviterOrganization)
            .WithMany()
            .HasForeignKey(i => i.InviterOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.AcceptedByUser)
            .WithMany()
            .HasForeignKey(i => i.AcceptedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.AcceptedOrganization)
            .WithMany()
            .HasForeignKey(i => i.AcceptedOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        // "My invitations" list, newest first, and the per-user daily send count.
        builder.HasIndex(i => new { i.InviterUserId, i.CreatedAt });

        // Per-recipient cooldown and the accept-by-email attribution fallback.
        builder.HasIndex(i => new { i.EmailCanonical, i.CreatedAt });

        // The reward caps: lifetime total and the rolling 30-day window, both per organisation.
        builder.HasIndex(i => new { i.InviterOrganizationId, i.RewardGrantedAt });

        // "One reward per invited organisation" — the guard against inviting the same person
        // from two of your own accounts.
        builder.HasIndex(i => i.AcceptedOrganizationId);

        // Deliberately NOT unique on (InviterUserId, EmailCanonical): a hard constraint would make
        // re-inviting after the cooldown impossible forever. The cooldown is a service rule, and a
        // re-invite creates a new row so the history stays honest.

        builder.ToTable("Invitations");
    }
}
