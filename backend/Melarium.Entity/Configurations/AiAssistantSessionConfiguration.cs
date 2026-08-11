using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class AiAssistantSessionConfiguration : IEntityTypeConfiguration<AiAssistantSession>
{
    public void Configure(EntityTypeBuilder<AiAssistantSession> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(80);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a hive keeps the session but detaches it — same policy as AdvisorConversation before it.
        builder.HasOne(s => s.Beehive)
            .WithMany()
            .HasForeignKey(s => s.BeehiveId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasMany(s => s.Turns)
            .WithOne(t => t.Session)
            .HasForeignKey(t => t.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.UserId);

        builder.ToTable("AiAssistantSessions");
    }
}
