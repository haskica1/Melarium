using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Seed;

/// <summary>
/// Provides deterministic seed data for development and initial deployment.
/// Uses static IDs so migrations are idempotent.
/// </summary>
public static class DataSeeder
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── Organizations ─────────────────────────────────────────────────────
        modelBuilder.Entity<Organization>().HasData(
            new Organization
            {
                Id = 1,
                Name = "Golden Hive Co",
                Description = "A family-run beekeeping operation in the lowlands, specialising in wildflower honey.",
                CreatedAt = now
            },
            new Organization
            {
                Id = 2,
                Name = "Mountain Bees",
                Description = "High-altitude apiculture collective producing premium acacia and linden honey.",
                CreatedAt = now
            }
        );

        // ── Apiaries ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Apiary>().HasData(
            new Apiary
            {
                Id = 1,
                Name = "Gorska Pčelinja",
                Description = "Mountain apiary located near the forest edge, known for acacia and linden honey.",
                OrganizationId = 2,
                CreatedAt = now
            },
            new Apiary
            {
                Id = 2,
                Name = "Dolinska Farma",
                Description = "Valley farm apiary with diverse flora — clover, sunflower, and wildflower.",
                OrganizationId = 1,
                CreatedAt = now
            }
        );

        // ── Beehives ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Beehive>().HasData(
            new Beehive
            {
                Id = 1,
                Name = "Košnica A1",
                Type = BeehiveType.Langstroth,
                Material = BeehiveMaterial.Wood,
                DateCreated = new DateTime(2022, 3, 15, 0, 0, 0, DateTimeKind.Utc),
                Notes = "Strong colony, productive queen introduced spring 2023.",
                ApiaryId = 1,
                CreatedAt = now
            },
            new Beehive
            {
                Id = 2,
                Name = "Košnica A2",
                Type = BeehiveType.DadantBlatt,
                Material = BeehiveMaterial.Wood,
                DateCreated = new DateTime(2022, 5, 20, 0, 0, 0, DateTimeKind.Utc),
                Notes = "Newer colony, monitoring for development.",
                ApiaryId = 1,
                CreatedAt = now
            },
            new Beehive
            {
                Id = 3,
                Name = "Košnica B1",
                Type = BeehiveType.Langstroth,
                Material = BeehiveMaterial.Polystyrene,
                DateCreated = new DateTime(2023, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                Notes = "Insulated polystyrene hive — excellent for winter survival.",
                ApiaryId = 2,
                CreatedAt = now
            },
            new Beehive
            {
                Id = 4,
                Name = "Košnica B2",
                Type = BeehiveType.Warré,
                Material = BeehiveMaterial.Wood,
                DateCreated = new DateTime(2023, 6, 5, 0, 0, 0, DateTimeKind.Utc),
                Notes = "Warré hive added for natural beekeeping trial.",
                ApiaryId = 2,
                CreatedAt = now
            }
        );

        // ── Inspections ───────────────────────────────────────────────────────
        modelBuilder.Entity<Inspection>().HasData(
            new Inspection
            {
                Id = 1,
                Date = new DateTime(2024, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                Temperature = 22.5,
                HoneyLevel = HoneyLevel.High,
                BroodStatus = "Healthy brood pattern. Queen spotted. Eggs and larvae present.",
                Notes = "Colony strong. Added super for honey storage.",
                BeehiveId = 1,
                CreatedAt = now
            },
            new Inspection
            {
                Id = 2,
                Date = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                Temperature = 28.0,
                HoneyLevel = HoneyLevel.Medium,
                BroodStatus = "Good brood. Some drone cells observed.",
                Notes = "Honey super 60% full. Will harvest next visit.",
                BeehiveId = 1,
                CreatedAt = now
            },
            new Inspection
            {
                Id = 3,
                Date = new DateTime(2024, 5, 12, 0, 0, 0, DateTimeKind.Utc),
                Temperature = 21.0,
                HoneyLevel = HoneyLevel.Low,
                BroodStatus = "Sparse brood. Queen activity low.",
                Notes = "Consider requeening if no improvement in 3 weeks.",
                BeehiveId = 2,
                CreatedAt = now
            },
            new Inspection
            {
                Id = 4,
                Date = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                Temperature = 25.5,
                HoneyLevel = HoneyLevel.Medium,
                BroodStatus = "Improving brood pattern. Queen productive.",
                Notes = "Colony recovering well.",
                BeehiveId = 3,
                CreatedAt = now
            }
        );
    }
}
