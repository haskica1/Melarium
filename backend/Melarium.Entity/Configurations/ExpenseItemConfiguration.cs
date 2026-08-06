using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class ExpenseItemConfiguration : IEntityTypeConfiguration<ExpenseItem>
{
    public void Configure(EntityTypeBuilder<ExpenseItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Unit)
            .HasMaxLength(50);

        builder.Property(i => i.Quantity)
            .HasColumnType("numeric(18,4)");

        builder.Property(i => i.UnitPrice)
            .HasColumnType("numeric(18,2)");

        builder.Property(i => i.TotalPrice)
            .HasColumnType("numeric(18,2)");

        builder.HasIndex(i => i.ExpenseId);

        // SPEC-12 Phase E — attribution to a feeding programme. SET NULL, never cascade: the
        // expense stays a true accounting record even after the programme it paid for is deleted.
        builder.HasOne(i => i.Diet)
            .WithMany()
            .HasForeignKey(i => i.DietId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => i.DietId);

        builder.ToTable("ExpenseItems");
    }
}
