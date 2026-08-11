using Melarium.Application.Features.Assistant;
using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>The hive-context block is the grounding the assistant answers questions from when a hive
/// is in scope (SPEC-18, originally SPEC-01's advisor), so its rendering and truncation must be
/// faithful and deterministic.</summary>
public class HiveContextBuilderTests
{
    private static Beehive Hive() => new()
    {
        Id = 1, Name = "Košnica 1", Type = BeehiveType.Langstroth, Material = BeehiveMaterial.Wood, ApiaryId = 3,
    };

    private static DietActiveInfo ActiveDiet(
        int dietId = 1, string name = "Zimsko hranjenje", FoodType food = FoodType.SugarSyrup,
        decimal? amount = null, FeedingAmountUnit? unit = null, string? amountNote = null,
        DateTime? next = null, int completed = 1, int total = 3) =>
        new(BeehiveId: 1, DietId: dietId, DietName: name, FoodType: food, CustomFoodType: null,
            AmountPerHive: amount, AmountUnit: unit, AmountNote: amountNote,
            StartDate: new DateTime(2026, 6, 1), NextFeedingDate: next,
            CompletedRounds: completed, TotalRounds: total);

    [Fact]
    public void Build_WithFullData_IncludesEverySection()
    {
        var inspections = new List<Inspection>
        {
            new() { Date = new DateTime(2026, 7, 1), HoneyLevel = HoneyLevel.High, BroodStatus = "Matica uočena", Notes = "Mirno društvo" },
        };
        var todos = new List<Todo> { new() { Title = "Dodati nastavak", Priority = TodoPriority.High, DueDate = new DateTime(2026, 7, 10) } };
        var queen = new Queen { Year = 2024, Status = QueenStatus.Active, Origin = QueenOrigin.Purchased };

        var treatment = new TreatmentLatestInfo(
            BeehiveId: 1, TreatmentId: 7, ProductName: "Apivar", ActiveSubstance: ActiveSubstance.Amitraz,
            Purpose: TreatmentPurpose.Varroa, Method: ApplicationMethod.Strips,
            StartDate: new DateTime(2026, 6, 1), EndDate: null, WithdrawalDays: 0);

        var text = HiveContextBuilder.Build(
            Hive(), "Pčelinjak Sjever", inspections, [ActiveDiet()], todos, queen, seasonYieldKg: 14.5m,
            latestTreatment: treatment, pastureLine: "Kadulja, od 01.06.2026", weatherLine: "12°C trenutno, danas 8–22°C");

        Assert.Contains("Košnica 1", text);
        Assert.Contains("Pčelinjak Sjever", text);
        Assert.Contains("Matica: godina 2024", text);      // queen section
        Assert.Contains("Prinos meda ove sezone: 14.5 kg", text);
        Assert.Contains("med Visoko", text);                // inspection with Bosnian honey label
        Assert.Contains("Matica uočena", text);
        Assert.Contains("Aktivna prehrana: Šećerni sirup (1/3 rundi)", text);
        Assert.Contains("Dodati nastavak", text);
        Assert.Contains("Zadnji tretman: Apivar (Amitraz), 01.06.2026, status: U toku", text);
        Assert.Contains("Pašnjak: Kadulja, od 01.06.2026", text);
        Assert.Contains("Vrijeme na pčelinjaku: 12°C trenutno, danas 8–22°C", text);
    }

    [Fact]
    public void Build_DietWithAmountAndNote_RendersBoth()
    {
        // "1 L 1:1" and "1 L 2:1" are the same litre and a different intervention — the note has to
        // reach the model, or the advice is grounded in half the fact.
        var text = HiveContextBuilder.Build(
            Hive(), "A", [], [ActiveDiet(amount: 1.5m, unit: FeedingAmountUnit.Litre, amountNote: "1:1",
                                        next: new DateTime(2026, 7, 12))],
            [], null, null, null, null, null);

        Assert.Contains("Aktivna prehrana: Šećerni sirup, 1.5 L (1:1) (1/3 rundi, sljedeće 12.07.2026)", text);
    }

    [Fact]
    public void Build_TwoOverlappingProgrammes_RendersOneLineEach()
    {
        var text = HiveContextBuilder.Build(
            Hive(), "A",
            [],
            [ActiveDiet(dietId: 1, food: FoodType.SugarSyrup), ActiveDiet(dietId: 2, food: FoodType.ProteinPatties)],
            [], null, null, null, null, null);

        Assert.Contains("Aktivna prehrana: Šećerni sirup", text);
        Assert.Contains("Aktivna prehrana: Proteinski kolači", text);
    }

    [Fact]
    public void Build_WithoutData_StatesNoInspectionsAndOmitsOptionalSections()
    {
        var text = HiveContextBuilder.Build(
            Hive(), "Pčelinjak A", [], [], [], activeQueen: null,
            seasonYieldKg: null, latestTreatment: null, pastureLine: null, weatherLine: null);

        Assert.Contains("Pregledi: nema zabilježenih pregleda.", text);
        Assert.DoesNotContain("Matica:", text);
        Assert.DoesNotContain("Aktivna prehrana", text);
        Assert.DoesNotContain("Prinos meda", text);
        Assert.DoesNotContain("Zadnji tretman", text);
        Assert.DoesNotContain("Pašnjak:", text);
        Assert.DoesNotContain("Vrijeme", text);
    }

    [Fact]
    public void Build_TruncatesLongInspectionText()
    {
        var longNote = new string('x', 300);
        var inspections = new List<Inspection>
        {
            new() { Date = new DateTime(2026, 7, 1), HoneyLevel = HoneyLevel.Low, BroodStatus = longNote },
        };

        var text = HiveContextBuilder.Build(
            Hive(), "A", inspections, [], [], null, null, null, null, null);

        Assert.Contains("…", text);
        Assert.DoesNotContain(longNote, text); // full 300-char string must not appear verbatim
    }

    [Fact]
    public void Build_TreatmentInKarenca_RendersKarencaUntilDate()
    {
        var treatment = new TreatmentLatestInfo(
            BeehiveId: 1, TreatmentId: 7, ProductName: "Oksalna kiselina", ActiveSubstance: ActiveSubstance.OxalicAcid,
            Purpose: TreatmentPurpose.Varroa, Method: ApplicationMethod.Trickling,
            StartDate: DateTime.UtcNow.AddDays(-10), EndDate: DateTime.UtcNow.AddDays(-2), WithdrawalDays: 30);

        var text = HiveContextBuilder.Build(
            Hive(), "A", [], [], [], null, null, treatment, null, null);

        var expectedUntil = treatment.EndDate!.Value.AddDays(30).ToString("dd.MM.yyyy");
        Assert.Contains($"Zadnji tretman: Oksalna kiselina (Oksalna kiselina)", text);
        Assert.Contains($"status: Karenca do {expectedUntil}", text);
    }
}
