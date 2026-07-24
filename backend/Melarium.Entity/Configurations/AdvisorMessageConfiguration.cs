using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class AdvisorMessageConfiguration : IEntityTypeConfiguration<AdvisorMessage>
{
    public void Configure(EntityTypeBuilder<AdvisorMessage> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role)
            .IsRequired();

        builder.Property(m => m.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.HasIndex(m => m.ConversationId);

        builder.ToTable("AdvisorMessages");
    }
}
