using Melarium.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Melarium.Entity.Configurations;

public class DietBeehiveConfiguration : IEntityTypeConfiguration<DietBeehive>
{
    public void Configure(EntityTypeBuilder<DietBeehive> builder)
    {
        builder.HasKey(x => x.Id);

        // Deleting a hive removes its programme links — same documented v1 trade-off as treatment
        // entries. The programme itself and its rounds survive.
        builder.HasOne(x => x.Beehive)
            .WithMany()
            .HasForeignKey(x => x.BeehiveId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DietId);
        builder.HasIndex(x => x.BeehiveId);

        // Partial, not plain: a hive may be on a programme only once *at a time*, but a removed hive
        // must stay re-addable. A plain unique index would collide with the old row forever.
        builder.HasIndex(x => new { x.DietId, x.BeehiveId })
            .IsUnique()
            .HasFilter("\"RemovedOn\" IS NULL");

        builder.ToTable("DietBeehives");
    }
}
