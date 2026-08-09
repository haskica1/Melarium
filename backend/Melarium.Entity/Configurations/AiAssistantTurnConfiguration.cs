using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class AiAssistantTurnConfiguration : IEntityTypeConfiguration<AiAssistantTurn>
{
    public void Configure(EntityTypeBuilder<AiAssistantTurn> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Role)
            .IsRequired();

        builder.Property(t => t.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(t => t.Transcript)
            .HasColumnType("text");

        builder.Property(t => t.RawModelJson)
            .HasColumnType("text");

        builder.Property(t => t.CandidatesJson)
            .HasColumnType("text");

        builder.HasMany(t => t.Actions)
            .WithOne(a => a.Turn)
            .HasForeignKey(a => a.TurnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.SessionId);

        // The monthly plan quota counts user turns per organization and filters on both columns.
        builder.HasIndex(t => new { t.Role, t.CreatedAt });

        builder.ToTable("AiAssistantTurns");
    }
}
