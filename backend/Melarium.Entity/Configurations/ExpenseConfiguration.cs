using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Currency)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.Property(e => e.TotalAmount)
            .HasColumnType("numeric(18,2)");

        builder.Property(e => e.Source)
            .IsRequired();

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(e => e.Organization)
            .WithMany()
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Items)
            .WithOne(i => i.Expense)
            .HasForeignKey(i => i.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.PurchaseDate);

        builder.ToTable("Expenses");
    }
}
