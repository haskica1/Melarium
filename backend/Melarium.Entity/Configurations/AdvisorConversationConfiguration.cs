using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class AdvisorConversationConfiguration : IEntityTypeConfiguration<AdvisorConversation>
{
    public void Configure(EntityTypeBuilder<AdvisorConversation> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(80);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a hive keeps the conversation but detaches it (context builder skips hive data,
        // UI chip shows "(obrisana)").
        builder.HasOne(c => c.Beehive)
            .WithMany()
            .HasForeignKey(c => c.BeehiveId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.UserId);

        builder.ToTable("AdvisorConversations");
    }
}
