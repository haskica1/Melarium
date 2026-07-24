using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class TodoConfiguration : IEntityTypeConfiguration<Todo>
{
    public void Configure(EntityTypeBuilder<Todo> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Notes)
            .HasMaxLength(1000);

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasDefaultValue(TodoPriority.Medium);

        builder.Property(t => t.IsCompleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // NoAction to avoid multiple cascade paths from Users (CreatedById already uses SetNull).
        // AssignedToId must be cleared in the service before a user can be deleted.
        builder.HasOne(t => t.AssignedTo)
            .WithMany()
            .HasForeignKey(t => t.AssignedToId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        // Optional FK to Apiary — NoAction to avoid multiple cascade paths from Apiary.
        // Apiary-level todos are deleted explicitly in ApiaryService.DeleteAsync.
        builder.HasOne(t => t.Apiary)
            .WithMany()
            .HasForeignKey(t => t.ApiaryId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        // Optional FK to Beehive
        builder.HasOne(t => t.Beehive)
            .WithMany()
            .HasForeignKey(t => t.BeehiveId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasIndex(t => t.ApiaryId);
        builder.HasIndex(t => t.BeehiveId);

        builder.ToTable("Todos");
    }
}
