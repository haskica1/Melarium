using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class QueenEditLogConfiguration : IEntityTypeConfiguration<QueenEditLog>
{
    public void Configure(EntityTypeBuilder<QueenEditLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.FieldLabel).IsRequired().HasMaxLength(100);
        builder.Property(l => l.OldValue).HasMaxLength(500);
        builder.Property(l => l.NewValue).HasMaxLength(500);

        builder.HasOne(l => l.Queen)
            .WithMany()
            .HasForeignKey(l => l.QueenId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.EditedBy)
            .WithMany()
            .HasForeignKey(l => l.EditedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(l => l.QueenId);

        builder.ToTable("QueenEditLogs");
    }
}
