using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class ApiaryMoveConfiguration : IEntityTypeConfiguration<ApiaryMove>
{
    public void Configure(EntityTypeBuilder<ApiaryMove> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.MovedAt).IsRequired();
        builder.Property(m => m.CertificateNumber).HasMaxLength(50);
        builder.Property(m => m.Notes).HasMaxLength(500);

        builder.HasOne(m => m.Apiary)
            .WithMany()
            .HasForeignKey(m => m.ApiaryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.FromPasture)
            .WithMany()
            .HasForeignKey(m => m.FromPastureId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // History is the point of the feature — a referenced pasture must not disappear.
        // Nullable: null ToPastureId = moved back to the matična lokacija (no pasture).
        builder.HasOne(m => m.ToPasture)
            .WithMany()
            .HasForeignKey(m => m.ToPastureId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(m => m.CreatedBy)
            .WithMany()
            .HasForeignKey(m => m.CreatedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(m => m.ApiaryId);
        builder.HasIndex(m => m.MovedAt);

        builder.ToTable("ApiaryMoves");
    }
}
