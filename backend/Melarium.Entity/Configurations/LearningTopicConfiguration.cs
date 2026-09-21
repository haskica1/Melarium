using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class LearningTopicConfiguration : IEntityTypeConfiguration<LearningTopic>
{
    public void Configure(EntityTypeBuilder<LearningTopic> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).IsRequired().HasMaxLength(150);
        builder.Property(t => t.Category).IsRequired();
        builder.Property(t => t.Summary).IsRequired().HasMaxLength(300);
        builder.Property(t => t.BodyMarkdown).IsRequired();
        builder.Property(t => t.Months); // Npgsql: integer[], nullable = evergreen
        builder.Property(t => t.VideoUrl).HasMaxLength(500);
        builder.Property(t => t.FileUrl).HasMaxLength(500);
        builder.Property(t => t.FileName).HasMaxLength(150);

        builder.Property(t => t.RejectionReason).HasMaxLength(500);

        // SetNull on both: a deleted account must not take a published article with it — the
        // attribution is what is lost, not the content (same rule as Feedback).
        builder.HasOne(t => t.Author)
            .WithMany()
            .HasForeignKey(t => t.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.ReviewedBy)
            .WithMany()
            .HasForeignKey(t => t.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.Reads)
            .WithOne(r => r.Topic)
            .HasForeignKey(r => r.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.IsPublished);
        builder.HasIndex(t => t.Category);
        builder.HasIndex(t => t.ReviewStatus);
        builder.HasIndex(t => t.AuthorId);

        builder.ToTable("LearningTopics");
    }
}
