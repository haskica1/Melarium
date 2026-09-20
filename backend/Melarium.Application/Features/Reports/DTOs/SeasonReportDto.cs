namespace Melarium.Application.Features.Reports.DTOs;

/// <summary>
/// One consolidated beekeeping report for an arbitrary period (SPEC-25). Both export formats — PDF
/// and Excel — are drawn from this one object (D11): the server computes, the client only renders,
/// so two documents for the same period cannot drift apart in a number.
/// </summary>
public record SeasonReportDto
{
    public ReportHeaderDto Header { get; init; } = new();
    public ReportYieldDto Yield { get; init; } = new();
    public ReportExpensesDto Expenses { get; init; } = new();
    public ReportBalanceDto Balance { get; init; } = new();
    public ReportTreatmentsDto Treatments { get; init; } = new();
    public ReportNotesDto Notes { get; init; } = new();
}

public record ReportHeaderDto
{
    public string OrganizationName { get; init; } = string.Empty;
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public DateTime GeneratedAt { get; init; }

    /// <summary>Apiaries actually covered — after role scoping and the downgrade lock (D12).</summary>
    public IReadOnlyList<string> ApiaryNames { get; init; } = [];
}

public record ReportYieldDto
{
    public decimal TotalKg { get; init; }

    /// <summary>Kg from harvests that carry a price per kg — the only kg behind the revenue estimate.</summary>
    public decimal PricedKg { get; init; }

    /// <summary>Kg with no price recorded. Counted separately so the estimate cannot lie by omission (D6).</summary>
    public decimal UnpricedKg { get; init; }

    public int HarvestCount { get; init; }

    public IReadOnlyList<NamedKgDto> ByApiary { get; init; } = [];
    public IReadOnlyList<NamedKgDto> ByHoneyType { get; init; } = [];
    public IReadOnlyList<NamedKgDto> ByBeehive { get; init; } = [];

    /// <summary>Yield attributed to the pasture the apiary stood on at harvest date (SPEC-10). Empty when no moves exist.</summary>
    public IReadOnlyList<NamedKgDto> ByPasture { get; init; } = [];
}

public record ReportExpensesDto
{
    public int Count { get; init; }

    /// <summary>Everything in the period, grouped by currency — never summed across currencies (D7).</summary>
    public IReadOnlyList<CurrencyAmountDto> ByCurrency { get; init; } = [];

    public IReadOnlyList<ApiaryExpenseDto> ByApiary { get; init; } = [];

    /// <summary>Expenses with no apiary — bought for the whole operation, not "unknown" (D1).</summary>
    public IReadOnlyList<CurrencyAmountDto> SharedByCurrency { get; init; } = [];

    /// <summary>Line items attributed to a feeding programme (SPEC-12 Phase E), per programme.</summary>
    public IReadOnlyList<DietExpenseDto> ByDiet { get; init; } = [];
}

public record ReportBalanceDto
{
    public decimal EstimatedRevenueBam { get; init; }
    public decimal TotalExpenseBam { get; init; }
    public decimal NetBam { get; init; }

    /// <summary>Per-apiary balance. Shared expenses are <b>not</b> spread over apiaries — they are reported on their own.</summary>
    public IReadOnlyList<ApiaryBalanceDto> ByApiary { get; init; } = [];
}

public record ReportTreatmentsDto
{
    public int Count { get; init; }

    /// <summary>Distinct hives that received at least one treatment in the period.</summary>
    public int HivesTreated { get; init; }

    /// <summary>Treatments still inside their withdrawal window as of the report's generation time.</summary>
    public int ActiveKarencaCount { get; init; }

    public IReadOnlyList<TreatmentProductDto> ByProduct { get; init; } = [];
}

/// <summary>
/// What the document must say out loud so a number is never read as more certain than it is.
/// </summary>
public record ReportNotesDto
{
    public decimal UnpricedKg { get; init; }
    public int UnassignedExpenseCount { get; init; }

    /// <summary>Currencies other than BAM present in the period; their amounts stay out of the balance.</summary>
    public IReadOnlyList<string> NonBamCurrencies { get; init; } = [];
}

public record NamedKgDto(string Name, decimal Kg);
public record CurrencyAmountDto(string Currency, decimal Amount);
public record ApiaryExpenseDto(int ApiaryId, string ApiaryName, IReadOnlyList<CurrencyAmountDto> ByCurrency);
public record DietExpenseDto(int DietId, string DietName, IReadOnlyList<CurrencyAmountDto> ByCurrency);
public record ApiaryBalanceDto(
    int ApiaryId,
    string ApiaryName,
    decimal Kg,
    decimal EstimatedRevenueBam,
    decimal ExpenseBam,
    decimal NetBam);
public record TreatmentProductDto(
    string ProductName,
    string ActiveSubstanceName,
    int TreatmentCount,
    int HiveCount);
