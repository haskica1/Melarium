using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Type)
            .IsRequired();

        builder.Property(f => f.Status)
            .IsRequired();

        builder.Property(f => f.Subject)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(f => f.Message)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(f => f.PageContext)
            .HasMaxLength(300);

        builder.Property(f => f.UserAgent)
            .HasMaxLength(300);

        builder.Property(f => f.ScreenshotStoragePath)
            .HasMaxLength(500);

        builder.Property(f => f.ScreenshotContentType)
            .HasMaxLength(100);

        builder.Property(f => f.AdminResponse)
            .HasMaxLength(1000);

        // SetNull on all three: a deleted account or organisation must not take the reported
        // problem with it. The report keeps its content, only the attribution is lost.
        builder.HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(f => f.Organization)
            .WithMany()
            .HasForeignKey(f => f.OrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(f => f.RespondedBy)
            .WithMany()
            .HasForeignKey(f => f.RespondedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(f => f.UserId);
        builder.HasIndex(f => f.Status);
        builder.HasIndex(f => f.Type);

        // The admin list and the user's own list are both newest-first.
        builder.HasIndex(f => f.CreatedAt);

        builder.ToTable("Feedbacks");
    }
}
