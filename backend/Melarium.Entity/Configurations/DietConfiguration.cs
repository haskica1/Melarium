using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class DietConfiguration : IEntityTypeConfiguration<Diet>
{
    public void Configure(EntityTypeBuilder<Diet> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.CustomReason)
            .HasMaxLength(500);

        builder.Property(d => d.CustomFoodType)
            .HasMaxLength(200);

        builder.Property(d => d.EarlyCompletionComment)
            .HasMaxLength(1000);

        builder.Property(d => d.Reason).IsRequired();
        builder.Property(d => d.FoodType).IsRequired();
        builder.Property(d => d.Status).IsRequired();
        builder.Property(d => d.StartDate).IsRequired();

        builder.HasOne(d => d.CreatedBy)
            .WithMany()
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // One diet → many feeding entries; cascade delete
        builder.HasMany(d => d.FeedingEntries)
            .WithOne(e => e.Diet)
            .HasForeignKey(e => e.DietId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.BeehiveId);

        builder.ToTable("Diets");
    }
}
