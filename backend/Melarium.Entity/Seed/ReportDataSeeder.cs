using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Melarium.Entity.Seed;

/// <summary>
/// Development-only sample data for the season report (SPEC-25) — harvests, expenses, treatments,
/// a feeding programme and a pasture move. The <c>InitialCreate</c> migration seeds organizations,
/// apiaries, hives and inspections but none of these, so <c>/reports</c> shows nothing but zeros on
/// a fresh database.
///
/// <para>
/// The rows are not decoration: each one exists to exercise a rule of the report that is easy to get
/// wrong and invisible when it is. See <see cref="SeedAsync"/> for which row proves what.
/// </para>
///
/// <para>
/// Same policy as <see cref="DatabaseInitializer.SeedUsersAsync"/> — Development only. No-ops as soon
/// as any harvest exists, so it never fights with data entered by hand.
/// </para>
/// </summary>
public static class ReportDataSeeder
{
    /// <summary>
    /// Europe/Sarajevo summer offset. Used for exactly one row — the late-evening harvest that must
    /// land inside its own month — and hardcoded rather than resolved because this project's
    /// <c>AppTimeZone</c> lives in the Application layer, which Entity does not reference. If the
    /// app's time zone ever stops being Sarajevo, that single row stops proving its point; nothing
    /// else here depends on it.
    /// </summary>
    private const int SarajevoSummerOffsetHours = 2;

    public static async Task SeedAsync(MelariumDbContext context)
    {
        if (await context.Harvests.AnyAsync()) return;

        // Resolved from the database, never hardcoded — the same reasoning as SeedUsersAsync: the
        // seeded apiaries may have been renamed, deleted or re-created with different ids.
        var golden = await ApiaryOfOrgAsync(context, organizationId: 1);
        var mountain = await ApiaryOfOrgAsync(context, organizationId: 2);
        if (golden is null) return;

        var goldenHives = await HiveIdsAsync(context, golden.Id);
        var mountainHives = mountain is null ? [] : await HiveIdsAsync(context, mountain.Id);
        if (goldenHives.Count == 0) return;

        var thisYear = DateTime.UtcNow.Year;
        var lastYear = thisYear - 1;

        await SeedPastureAndMoveAsync(context, golden, thisYear);
        var dietId = await SeedFeedingProgrammeAsync(context, golden, goldenHives, thisYear);

        SeedHarvests(context, golden, goldenHives, mountain, mountainHives, thisYear, lastYear);
        SeedExpenses(context, golden, mountain, dietId, thisYear, lastYear);
        SeedTreatments(context, golden, goldenHives, thisYear);

        await context.SaveChangesAsync();
    }

    // ── Harvests ───────────────────────────────────────────────────────────────

    private static void SeedHarvests(
        MelariumDbContext context,
        Apiary golden, List<int> goldenHives,
        Apiary? mountain, List<int> mountainHives,
        int thisYear, int lastYear)
    {
        Harvest H(int apiaryId, DateTime date, HoneyType type, decimal? pricePerKg, string? notes,
                  IEnumerable<(int HiveId, decimal Kg)> entries) => new()
        {
            ApiaryId = apiaryId,
            Date = date,
            HoneyType = type,
            PricePerKg = pricePerKg,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            Entries = entries
                .Select(e => new HarvestEntry { BeehiveId = e.HiveId, QuantityKg = e.Kg, CreatedAt = DateTime.UtcNow })
                .ToList(),
        };

        var a = goldenHives[0];
        var b = goldenHives.Count > 1 ? goldenHives[1] : goldenHives[0];

        context.Harvests.AddRange(
            // Current season — priced, so it carries the revenue estimate.
            H(golden.Id, Utc(thisYear, 5, 28), HoneyType.Acacia, 14.00m, "Bagremova paša, prvo vrcanje.",
                [(a, 26.5m), (b, 21.0m)]),

            H(golden.Id, Utc(thisYear, 6, 24), HoneyType.Linden, 12.50m, "Lipa, dobra paša.",
                [(a, 18.0m), (b, 15.5m)]),

            // ── No price recorded. Proves the "Procijenjeni prihod" note (SPEC-25 D6): these kg are
            // in TotalKg and in unpricedKg, and contribute nothing to revenue. Without a row like
            // this the note never renders and the omission is never visible on screen.
            H(golden.Id, Utc(thisYear, 7, 19), HoneyType.Meadow, null, "Livadski — cijena još nije dogovorena.",
                [(a, 12.0m), (b, 9.5m)]),

            // ── 23:30 local on the last day of September. Stored as 21:30Z, which is the same
            // calendar day in Sarajevo and the *next* one in UTC. Proves ReportPeriod (D4): pick
            // "Septembar" and this must be inside it; a UTC-only comparison would push it to October
            // and quietly shrink the month.
            H(golden.Id,
                new DateTime(thisYear, 9, 30, 23 - SarajevoSummerOffsetHours, 30, 0, DateTimeKind.Utc),
                HoneyType.Forest, 16.00m, "Kasno vrcanje — namjerno na granici mjeseca (test lokalne zone).",
                [(a, 7.5m)]),

            // Previous season — makes the "2025" and year-over-year presets non-empty.
            H(golden.Id, Utc(lastYear, 6, 2), HoneyType.Acacia, 13.00m, "Prošlogodišnji bagrem.",
                [(a, 22.0m), (b, 19.0m)]),
            H(golden.Id, Utc(lastYear, 7, 15), HoneyType.Meadow, 11.00m, null,
                [(a, 14.0m), (b, 11.0m)])
        );

        // A second organization, so cross-tenant scoping is testable: signed in as Golden Hive, none
        // of this may appear in the report.
        if (mountain is not null && mountainHives.Count > 0)
        {
            context.Harvests.Add(
                H(mountain.Id, Utc(thisYear, 6, 10), HoneyType.Chestnut, 18.00m, "Kesten — druga organizacija.",
                    [(mountainHives[0], 31.0m)]));
        }
    }

    // ── Expenses ───────────────────────────────────────────────────────────────

    private static void SeedExpenses(
        MelariumDbContext context,
        Apiary golden, Apiary? mountain, int? dietId, int thisYear, int lastYear)
    {
        Expense E(int organizationId, int? apiaryId, DateTime date, string currency, string? notes,
                  IEnumerable<(string Name, decimal Qty, string? Unit, decimal UnitPrice)> items)
        {
            var lines = items.Select((i, idx) => new ExpenseItem
            {
                Name = i.Name,
                Quantity = i.Qty,
                Unit = i.Unit,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.Qty * i.UnitPrice,
                SortOrder = idx,
                CreatedAt = DateTime.UtcNow,
            }).ToList();

            return new Expense
            {
                OrganizationId = organizationId,
                ApiaryId = apiaryId,
                Source = ExpenseSource.Manual,
                PurchaseDate = date,
                TotalAmount = lines.Sum(l => l.TotalPrice),
                Currency = currency,
                Notes = notes,
                CreatedAt = DateTime.UtcNow,
                Items = lines,
            };
        }

        // Tied to the apiary — this is what puts a cost next to a yield in the per-apiary balance.
        var sugar = E(golden.OrganizationId, golden.Id, Utc(thisYear, 8, 12), "BAM", "Zimska prehrana, šećer.",
            [("Šećer kristal", 50m, "kg", 1.85m)]);

        // Attributed to the feeding programme as well — populates "Prehrana po programu". The apiary
        // on the expense must match the programme's apiary, or the service refuses it with 400 (D2).
        if (dietId is int id) sugar.Items[0].DietId = id;

        context.Expenses.AddRange(
            sugar,

            E(golden.OrganizationId, golden.Id, Utc(thisYear, 4, 3), "BAM", "Okviri i satne osnove.",
                [("Satna osnova", 8m, "kg", 22.00m), ("Okvir LR", 40m, "kom", 1.20m)]),

            // ── No apiary: a shared expense (SPEC-25 D1). NULL means "bought for the whole
            // operation", not "unknown". Proves that it stays out of the per-apiary balance but
            // still counts org-wide, and that the "N računa nije vezano..." note renders.
            E(golden.OrganizationId, null, Utc(thisYear, 3, 20), "BAM", "Zajednička oprema za sve pčelinjake.",
                [("Centrifuga, servis", 1m, "kom", 180.00m), ("Dimilica", 2m, "kom", 45.00m)]),

            E(golden.OrganizationId, null, Utc(thisYear, 5, 9), "BAM", "Zaštitna oprema.",
                [("Pčelarsko odijelo", 2m, "kom", 95.00m)]),

            // ── A different currency. Proves D7: currencies are listed separately, never summed,
            // and this amount stays out of the KM balance with the document saying so.
            E(golden.OrganizationId, golden.Id, Utc(thisYear, 5, 22), "EUR", "Matice iz uvoza.",
                [("Matica Carnica", 3m, "kom", 28.00m)]),

            E(golden.OrganizationId, golden.Id, Utc(lastYear, 8, 18), "BAM", "Prošlogodišnja prehrana.",
                [("Šećer kristal", 40m, "kg", 1.75m)])
        );

        if (mountain is not null)
        {
            context.Expenses.Add(
                E(mountain.OrganizationId, mountain.Id, Utc(thisYear, 4, 28), "BAM", "Druga organizacija.",
                    [("Lijek protiv varoe", 10m, "kom", 6.50m)]));
        }
    }

    // ── Treatments ─────────────────────────────────────────────────────────────

    private static void SeedTreatments(MelariumDbContext context, Apiary golden, List<int> hives, int thisYear)
    {
        Treatment T(DateTime start, DateTime? end, TreatmentPurpose purpose, string product,
                    ActiveSubstance substance, ApplicationMethod method, string dose,
                    int withdrawalDays, int rounds, int intervalDays, string? batch, string? notes)
        {
            var t = new Treatment
            {
                ApiaryId = golden.Id,
                Purpose = purpose,
                ProductName = product,
                ActiveSubstance = substance,
                Method = method,
                DosePerHive = dose,
                StartDate = start,
                EndDate = end,
                WithdrawalDays = withdrawalDays,
                TotalRounds = rounds,
                IntervalDays = intervalDays,
                BatchNumber = batch,
                Supplier = "Veterinarska apoteka",
                Notes = notes,
                CreatedAt = DateTime.UtcNow,
                Entries = hives
                    .Select(h => new TreatmentEntry { BeehiveId = h, CreatedAt = DateTime.UtcNow })
                    .ToList(),
            };

            t.Rounds = Enumerable.Range(0, rounds)
                .Select(i => new TreatmentRound
                {
                    ScheduledDate = start.AddDays(i * intervalDays),
                    Status = end is null ? TreatmentRoundStatus.Pending : TreatmentRoundStatus.Completed,
                    CompletionDate = end is null ? null : start.AddDays(i * intervalDays),
                    CreatedAt = DateTime.UtcNow,
                })
                .ToList();

            return t;
        }

        context.Treatments.AddRange(
            // ── Starts 25 September, ends 5 October. Proves D5: it belongs to September and to Q3
            // only. Run the Q3 and Q4 reports and it must appear in exactly one of them — otherwise
            // the four quarters stop adding up to the year.
            T(Utc(thisYear, 9, 25), Utc(thisYear, 10, 5), TreatmentPurpose.Varroa, "Apiguard",
                ActiveSubstance.Thymol, ApplicationMethod.Evaporation, "1 posudica (50 g) po košnici",
                withdrawalDays: 0, rounds: 2, intervalDays: 14, batch: "APG-2431",
                notes: "Namjerno prelazi granicu mjeseca — test pravila po datumu početka."),

            T(Utc(thisYear, 8, 1), Utc(thisYear, 8, 29), TreatmentPurpose.Varroa, "Amitraz trake",
                ActiveSubstance.Amitraz, ApplicationMethod.Strips, "2 trake po košnici",
                withdrawalDays: 30, rounds: 1, intervalDays: 0, batch: "AMZ-1187",
                notes: "Ljetni tretman odmah nakon vrcanja."),

            // ── No EndDate: still running, so the report's "U karenci" / status derivation has
            // something live to compute against whenever this is seeded.
            T(Utc(thisYear, 12, 1), null, TreatmentPurpose.Varroa, "Oksalna kiselina 3,2%",
                ActiveSubstance.OxalicAcid, ApplicationMethod.Trickling, "5 ml po ulici pčela",
                withdrawalDays: 0, rounds: 1, intervalDays: 0, batch: "OXA-0042",
                notes: "Zimsko nakapavanje na bezleglo stanje.")
        );
    }

    // ── Pasture + move (SPEC-10) ───────────────────────────────────────────────

    private static async Task SeedPastureAndMoveAsync(MelariumDbContext context, Apiary golden, int thisYear)
    {
        if (await context.Pastures.AnyAsync(p => p.OrganizationId == golden.OrganizationId)) return;

        var pasture = new Pasture
        {
            OrganizationId = golden.OrganizationId,
            Name = "Vlašić — bagremova paša",
            Latitude = 44.3100,
            Longitude = 17.6400,
            Address = "Vlašić, Travnik",
            FloraNotes = "Bagrem, kasnije livada; paša traje V–VI.",
            CreatedAt = DateTime.UtcNow,
        };
        context.Pastures.Add(pasture);
        await context.SaveChangesAsync();

        // Moved in mid-May: the acacia harvest at the end of May is attributed to the pasture, and
        // everything before it falls into the "Matična lokacija" bucket. Without a move the whole
        // "Po pašnjaku" section is empty by design and cannot be tested.
        context.ApiaryMoves.Add(new ApiaryMove
        {
            ApiaryId = golden.Id,
            FromPastureId = null,
            ToPastureId = pasture.Id,
            MovedAt = Utc(thisYear, 5, 15),
            CertificateNumber = "VET-2024-0917",
            Notes = "Selidba na bagrem.",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    // ── Feeding programme (SPEC-12) ────────────────────────────────────────────

    /// <summary>Returns the diet id so an expense line can be attributed to it, or null if one already exists.</summary>
    private static async Task<int?> SeedFeedingProgrammeAsync(
        MelariumDbContext context, Apiary golden, List<int> hives, int thisYear)
    {
        if (await context.Diets.AnyAsync(d => d.ApiaryId == golden.Id)) return null;

        var diet = new Diet
        {
            ApiaryId = golden.Id,
            Name = $"Zimska prehrana {thisYear}",
            StartDate = Utc(thisYear, 8, 15),
            Reason = DietReason.WinterFeeding,
            DurationDays = 21,
            FrequencyDays = 7,
            FoodType = FoodType.SugarSyrup,
            AmountPerHive = 3m,
            AmountUnit = FeedingAmountUnit.Litre,
            AmountNote = "2:1",
            Status = DietStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            Beehives = hives
                .Select(h => new DietBeehive { BeehiveId = h, CreatedAt = DateTime.UtcNow })
                .ToList(),
        };

        context.Diets.Add(diet);
        await context.SaveChangesAsync();
        return diet.Id;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<Apiary?> ApiaryOfOrgAsync(MelariumDbContext context, int organizationId) =>
        await context.Apiaries
            .Where(a => a.OrganizationId == organizationId)
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync();

    private static async Task<List<int>> HiveIdsAsync(MelariumDbContext context, int apiaryId) =>
        await context.Beehives
            .Where(b => b.ApiaryId == apiaryId && b.MergedIntoBeehiveId == null)
            .OrderBy(b => b.Id)
            .Select(b => b.Id)
            .ToListAsync();
}
