namespace Melarium.Application.Features.Reports.DTOs;

/// <summary>
/// One consolidated beekeeping report for an arbitrary period (SPEC-25). Both export formats — PDF
/// and Excel — are drawn from this one object (D11): the server computes, the client only renders,
/// so two documents for the same period cannot drift apart in a number.
/// </summary>
public record SeasonReportDto
{
    public ReportHeaderDto Header { get; init; } = new();

    /// <summary>
    /// Every product of the period side by side, honey first — the overview that opens "Prinosi"
    /// (SPEC-30). Its revenue is the one figure summed across products; quantities never are.
    /// </summary>
    public ReportHarvestsDto Harvests { get; init; } = new();

    /// <summary>Honey only — the meaning it has had since SPEC-25. Comb honey is a product, not honey.</summary>
    public ReportYieldDto Yield { get; init; } = new();

    /// <summary>Bee products other than honey (SPEC-30).</summary>
    public ReportProductsDto Products { get; init; } = new();

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

/// <summary>
/// One row per product with a record in the period (SPEC-30), in enum order so the rows do not
/// reshuffle when a client renders them in another language — honey first.
/// </summary>
public record ReportHarvestsDto
{
    public IReadOnlyList<ProductTypeReportDto> ByProduct { get; init; } = [];

    /// <summary>
    /// Honey and every other product: <see cref="ReportBalanceDto.EstimatedRevenueBam"/> +
    /// <see cref="ReportBalanceDto.ProductRevenueBam"/>. The only total across products.
    /// </summary>
    public decimal EstimatedRevenueBam { get; init; }
}

/// <summary>
/// Bee products other than honey in the period (SPEC-30), broken down like honey. Quantities are never
/// summed across types — a gram of royal jelly and a kilo of wax make no total — so every row lists
/// each product on its own, and there is no "total kg" anywhere.
/// </summary>
public record ReportProductsDto
{
    public int RecordCount { get; init; }

    /// <summary>Per apiary; records of the whole organization form their own, last row.</summary>
    public IReadOnlyList<ApiaryProductsReportDto> ByApiary { get; init; } = [];

    /// <summary>
    /// Per pasture the apiary stood on at the record's date — the honey rule (SPEC-10). Records of the
    /// whole organization form their own row. Empty when the organization records no moves.
    /// </summary>
    public IReadOnlyList<NamedProductsReportDto> ByPasture { get; init; } = [];

    /// <summary>
    /// Per hive, from per-hive lines only: a record kept as one figure has no hive to be put on, and
    /// <see cref="ReportNotesDto.NotPerHive"/> says how much that is.
    /// </summary>
    public IReadOnlyList<NamedProductsReportDto> ByBeehive { get; init; } = [];
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
    /// <summary>Honey only — the meaning this field has had since SPEC-25.</summary>
    public decimal EstimatedRevenueBam { get; init; }

    /// <summary>Other bee products (SPEC-30), shared records included — they are the organization's income.</summary>
    public decimal ProductRevenueBam { get; init; }

    public decimal TotalExpenseBam { get; init; }

    /// <summary>Honey revenue + product revenue − BAM expenses.</summary>
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

    /// <summary>
    /// Per product, honey included, the quantity recorded without a price and therefore missing from
    /// the revenue estimate (SPEC-30). <see cref="UnpricedKg"/> is its honey row, kept for older clients.
    /// </summary>
    public IReadOnlyList<ProductKgReportDto> Unpriced { get; init; } = [];

    /// <summary>
    /// Per product, honey included, the quantity recorded as one figure for an apiary or the
    /// organization: in every total, in no per-hive table (SPEC-30).
    /// </summary>
    public IReadOnlyList<ProductKgReportDto> NotPerHive { get; init; } = [];

    /// <summary>Records of the whole organization, honey and products: in the total balance, in no apiary's row.</summary>
    public int SharedHarvestCount { get; init; }
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
    decimal ProductRevenueBam,
    decimal ExpenseBam,
    decimal NetBam);
public record ProductTypeReportDto(
    Domain.Enums.HiveProductType ProductType,
    string Name,
    decimal Kg,
    decimal PricedKg,
    decimal UnpricedKg,
    decimal EstimatedRevenueBam,
    int RecordCount);
public record ApiaryProductsReportDto(int? ApiaryId, string ApiaryName, IReadOnlyList<ProductKgReportDto> Items);

/// <summary>A pasture's or a hive's products, each on its own (SPEC-30).</summary>
public record NamedProductsReportDto(string Name, IReadOnlyList<ProductKgReportDto> Items);
public record ProductKgReportDto(Domain.Enums.HiveProductType ProductType, string Name, decimal Kg);
public record TreatmentProductDto(
    string ProductName,
    string ActiveSubstanceName,
    int TreatmentCount,
    int HiveCount);
