using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class AiAssistantActionConfiguration : IEntityTypeConfiguration<AiAssistantAction>
{
    public void Configure(EntityTypeBuilder<AiAssistantAction> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Kind)
            .IsRequired();

        builder.Property(a => a.Status)
            .IsRequired();

        builder.Property(a => a.PayloadJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(a => a.TargetSummary)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.ResultEntityType)
            .HasMaxLength(50);

        builder.Property(a => a.ErrorMessage)
            .HasMaxLength(500);

        // ResultEntityId is deliberately not a foreign key — deleting the created inspection must
        // not delete the audit row recording that the assistant created it (SPEC-17 §6).

        builder.HasIndex(a => a.TurnId);

        builder.ToTable("AiAssistantActions");
    }
}
