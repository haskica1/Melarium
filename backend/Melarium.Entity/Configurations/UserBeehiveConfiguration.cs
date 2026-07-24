using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class UserBeehiveConfiguration : IEntityTypeConfiguration<UserBeehive>
{
    public void Configure(EntityTypeBuilder<UserBeehive> builder)
    {
        builder.HasKey(ub => new { ub.UserId, ub.BeehiveId });

        builder.HasOne(ub => ub.User)
            .WithMany(u => u.AssignedBeehives)
            .HasForeignKey(ub => ub.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ub => ub.Beehive)
            .WithMany(b => b.AssignedUsers)
            .HasForeignKey(ub => ub.BeehiveId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("UserBeehives");
    }
}
