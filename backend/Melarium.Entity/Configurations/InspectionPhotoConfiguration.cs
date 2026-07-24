using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class InspectionPhotoConfiguration : IEntityTypeConfiguration<InspectionPhoto>
{
    public void Configure(EntityTypeBuilder<InspectionPhoto> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.StoragePath)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(p => p.ContentType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.SizeBytes)
            .IsRequired();

        builder.Property(p => p.Caption)
            .HasMaxLength(200);

        // AnalysisJson intentionally unbounded (text) — structured AI output, size varies.

        builder.HasOne(p => p.Inspection)
            .WithMany(i => i.Photos)
            .HasForeignKey(p => p.InspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.InspectionId);

        builder.ToTable("InspectionPhotos");
    }
}
