using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Localization;

/// <summary>
/// Bosnian display labels for domain enums. These are the UI-facing strings the
/// API returns in <c>*Name</c> fields and in statistics, so the whole app shows
/// Bosnian text. Keep these in sync with the frontend label maps in
/// <c>frontend/src/core/models/index.ts</c>.
/// </summary>
public static class BsLabels
{
    public static string Label(FoodType ft) => ft switch
    {
        FoodType.SugarSyrup     => "Šećerni sirup",
        FoodType.Fondant        => "Fondan",
        FoodType.Pollen         => "Polen",
        FoodType.ProteinPatties => "Proteinski kolači",
        FoodType.Custom         => "Vlastito",
        _                       => ft.ToString(),
    };

    public static string Label(DietReason r) => r switch
    {
        DietReason.LackOfFood               => "Nedostatak hrane",
        DietReason.WinterFeeding            => "Zimsko hranjenje",
        DietReason.SpringStimulation        => "Proljetna stimulacija",
        DietReason.NewSwarmSupport          => "Podrška novom roju",
        DietReason.PostHarvestRecovery      => "Oporavak nakon berbe",
        DietReason.DroughtConditions        => "Uvjeti suše",
        DietReason.WeakColonySupport        => "Podrška slaboj koloniji",
        DietReason.QueenIntroductionSupport => "Podrška uvođenju matice",
        DietReason.Custom                   => "Vlastito",
        _                                   => r.ToString(),
    };

    public static string Label(DietStatus s) => s switch
    {
        DietStatus.NotStarted   => "Nije započeto",
        DietStatus.InProgress   => "U toku",
        DietStatus.Completed    => "Završeno",
        DietStatus.StoppedEarly => "Prijevremeno prekinuto",
        _                       => s.ToString(),
    };

    public static string Label(FeedingEntryStatus s) => s switch
    {
        FeedingEntryStatus.Pending   => "Na čekanju",
        FeedingEntryStatus.Completed => "Završeno",
        _                            => s.ToString(),
    };

    public static string Label(HoneyLevel h) => h switch
    {
        HoneyLevel.Low    => "Nisko",
        HoneyLevel.Medium => "Srednje",
        HoneyLevel.High   => "Visoko",
        _                 => h.ToString(),
    };

    public static string Label(PlanType p) => p switch
    {
        PlanType.Free     => "Besplatni",
        PlanType.Standard => "Standard",
        PlanType.Pro      => "Pro",
        PlanType.Max      => "Max",
        PlanType.Partner  => "Partner",
        _                 => p.ToString(),
    };

    public static string Label(TodoPriority p) => p switch
    {
        TodoPriority.Low    => "Nizak",
        TodoPriority.Medium => "Srednji",
        TodoPriority.High   => "Visok",
        _                   => p.ToString(),
    };

    public static string Label(BeehiveType t) => t switch
    {
        BeehiveType.Langstroth  => "LR (Langstroth-Rutova) košnica",
        BeehiveType.DadantBlatt => "DB (Dadan-Blatt) košnica",
        BeehiveType.Warré       => "AŽ (Alberti-Žnideršič) košnica",
        BeehiveType.TopBar      => "Pološka košnica",
        BeehiveType.Other       => "Ostalo",
        _                       => t.ToString(),
    };

    public static string Label(QueenMarkColor c) => c switch
    {
        QueenMarkColor.White  => "Bijela",
        QueenMarkColor.Yellow => "Žuta",
        QueenMarkColor.Red    => "Crvena",
        QueenMarkColor.Green  => "Zelena",
        QueenMarkColor.Blue   => "Plava",
        _                     => c.ToString(),
    };

    public static string Label(QueenOrigin o) => o switch
    {
        QueenOrigin.Purchased   => "Kupljena",
        QueenOrigin.OwnBreeding => "Vlastiti uzgoj",
        QueenOrigin.Swarm       => "Rojenje",
        QueenOrigin.Supersedure => "Tiha zamjena",
        QueenOrigin.Unknown     => "Nepoznato",
        _                       => o.ToString(),
    };

    public static string Label(QueenStatus s) => s switch
    {
        QueenStatus.Active   => "Aktivna",
        QueenStatus.Replaced => "Zamijenjena",
        QueenStatus.Died     => "Uginula",
        QueenStatus.Missing  => "Nestala",
        _                    => s.ToString(),
    };

    public static string Label(BeehiveMaterial m) => m switch
    {
        BeehiveMaterial.Wood        => "Drvo",
        BeehiveMaterial.Plastic     => "Plastika",
        BeehiveMaterial.Polystyrene => "Stiropor",
        _                           => m.ToString(),
    };

    public static string Label(HoneyType t) => t switch
    {
        HoneyType.Acacia    => "Bagrem",
        HoneyType.Linden    => "Lipa",
        HoneyType.Chestnut  => "Kesten",
        HoneyType.Sunflower => "Suncokret",
        HoneyType.Meadow    => "Livadski",
        HoneyType.Forest    => "Šumski",
        HoneyType.Rapeseed  => "Uljana repica",
        HoneyType.Other     => "Ostalo",
        _                   => t.ToString(),
    };

    public static string Label(TreatmentPurpose p) => p switch
    {
        TreatmentPurpose.Varroa     => "Varoa",
        TreatmentPurpose.Nosema     => "Nozemoza",
        TreatmentPurpose.Chalkbrood => "Krečno leglo",
        TreatmentPurpose.Other      => "Ostalo",
        _                           => p.ToString(),
    };

    public static string Label(ActiveSubstance s) => s switch
    {
        ActiveSubstance.Amitraz        => "Amitraz",
        ActiveSubstance.Flumethrin     => "Flumetrin",
        ActiveSubstance.TauFluvalinate => "Tau-fluvalinat",
        ActiveSubstance.Coumaphos      => "Kumafos",
        ActiveSubstance.OxalicAcid     => "Oksalna kiselina",
        ActiveSubstance.FormicAcid     => "Mravlja kiselina",
        ActiveSubstance.Thymol         => "Timol",
        ActiveSubstance.Other          => "Ostalo",
        _                              => s.ToString(),
    };

    public static string Label(ApplicationMethod m) => m switch
    {
        ApplicationMethod.Strips      => "Trake",
        ApplicationMethod.Trickling   => "Nakapavanje",
        ApplicationMethod.Sublimation => "Sublimacija",
        ApplicationMethod.Evaporation => "Isparavanje",
        ApplicationMethod.InFeed      => "U prihrani",
        ApplicationMethod.Spraying    => "Prskanje",
        ApplicationMethod.Other       => "Ostalo",
        _                             => m.ToString(),
    };

    public static string Label(TreatmentStatus s) => s switch
    {
        TreatmentStatus.InProgress => "U toku",
        TreatmentStatus.Karenca    => "Karenca",
        TreatmentStatus.Completed  => "Završen",
        _                          => s.ToString(),
    };

    public static string Label(LearningCategory c) => c switch
    {
        LearningCategory.Osnove            => "Osnove",
        LearningCategory.SezonskiRadovi    => "Sezonski radovi",
        LearningCategory.BolestiINametnici => "Bolesti i nametnici",
        LearningCategory.Oprema            => "Oprema",
        LearningCategory.Propisi           => "Propisi",
        LearningCategory.Napredno          => "Napredno",
        _                                  => c.ToString(),
    };

    public static string Label(FeedbackType t) => t switch
    {
        FeedbackType.Bug            => "Prijava problema",
        FeedbackType.Complaint      => "Žalba",
        FeedbackType.Compliment     => "Pohvala",
        FeedbackType.FeatureRequest => "Prijedlog",
        FeedbackType.Question       => "Pitanje",
        FeedbackType.Other          => "Ostalo",
        _                           => t.ToString(),
    };

    public static string Label(FeedbackSeverity s) => s switch
    {
        FeedbackSeverity.Low      => "Nizak",
        FeedbackSeverity.Medium   => "Srednji",
        FeedbackSeverity.High     => "Visok",
        FeedbackSeverity.Critical => "Kritičan",
        _                         => s.ToString(),
    };

    public static string Label(FeedbackStatus s) => s switch
    {
        FeedbackStatus.New       => "Novo",
        FeedbackStatus.InReview  => "U razmatranju",
        FeedbackStatus.Resolved  => "Riješeno",
        FeedbackStatus.Dismissed => "Odbijeno",
        _                        => s.ToString(),
    };

    private static readonly string[] MonthsShort =
        { "jan", "feb", "mar", "apr", "maj", "jun", "jul", "avg", "sep", "okt", "nov", "dec" };

    /// <summary>Short Bosnian month label with 2-digit year, e.g. "maj 25".</summary>
    public static string MonthShort(int year, int month) =>
        $"{MonthsShort[month - 1]} {year % 100:00}";
}
