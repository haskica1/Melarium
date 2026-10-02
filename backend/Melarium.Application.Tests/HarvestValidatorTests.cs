using Melarium.Application.Features.Harvests.DTOs;
using Melarium.Application.Features.Harvests.Validators;
using Melarium.Domain.Enums;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// SPEC-30: a quantity is recorded at exactly one level — per hive or one figure — honey needs its type,
/// a client that sends no product means honey, and venom is weighed in milligrams.
/// </summary>
public class HarvestValidatorTests
{
    private const int Apiary = 1;

    private readonly CreateHarvestValidator _create = new();
    private readonly UpdateHarvestValidator _update = new();

    private static CreateHarvestDto Create(
        int? apiaryId, decimal? bulkKg, HiveProductType? product, params (int Hive, decimal Kg)[] entries) => new()
    {
        ApiaryId = apiaryId, Date = DateTime.UtcNow.AddDays(-1), ProductType = product,
        HoneyType = product is null or HiveProductType.Honey ? HoneyType.Linden : null,
        BulkKg = bulkKg,
        Entries = entries.Select(e => new CreateHarvestEntryDto { BeehiveId = e.Hive, QuantityKg = e.Kg }).ToList(),
    };

    [Fact]
    public void OlderClient_HoneyPerHive_WithoutAProduct_IsValid() =>
        Assert.True(_create.Validate(Create(Apiary, null, null, (5, 20m), (6, 18.5m))).IsValid);

    [Fact]
    public void Honey_AsOneFigureForTheApiary_IsValid() =>
        Assert.True(_create.Validate(Create(Apiary, 300m, HiveProductType.Honey)).IsValid);

    [Fact]
    public void Honey_WithoutHoneyType_IsInvalid()
    {
        var dto = Create(Apiary, 300m, HiveProductType.Honey);
        dto.HoneyType = null;
        Assert.False(_create.Validate(dto).IsValid);
    }

    [Fact]
    public void OtherProduct_NeedsNoHoneyType() =>
        Assert.True(_create.Validate(Create(Apiary, 1.5m, HiveProductType.Wax)).IsValid);

    [Fact]
    public void BothLevels_IsInvalid() =>
        Assert.False(_create.Validate(Create(Apiary, 1m, HiveProductType.Pollen, (5, 0.2m))).IsValid);

    [Fact]
    public void NoQuantity_IsInvalid() =>
        Assert.False(_create.Validate(Create(Apiary, null, HiveProductType.Pollen)).IsValid);

    [Fact]
    public void WholeOrganization_CannotBeSplitPerHive() =>
        Assert.False(_create.Validate(Create(null, null, HiveProductType.Wax, (5, 0.2m))).IsValid);

    [Fact]
    public void WholeOrganization_OneFigure_IsValid_HoneyToo()
    {
        Assert.True(_create.Validate(Create(null, 12m, HiveProductType.Wax)).IsValid);
        Assert.True(_create.Validate(Create(null, 400m, HiveProductType.Honey)).IsValid);
    }

    [Fact]
    public void SameHiveTwice_IsInvalid() =>
        Assert.False(_create.Validate(Create(Apiary, null, HiveProductType.Propolis, (5, 0.2m), (5, 0.1m))).IsValid);

    [Fact]
    public void OneMilligram_IsAccepted() =>
        Assert.True(_create.Validate(Create(Apiary, 0.000001m, HiveProductType.BeeVenom)).IsValid);

    [Fact]
    public void ZeroQuantity_IsInvalid() =>
        Assert.False(_create.Validate(Create(Apiary, 0m, HiveProductType.Wax)).IsValid);

    [Fact]
    public void PriceAboveTheColumn_IsInvalid()
    {
        var dto = Create(Apiary, 0.001m, HiveProductType.BeeVenom);
        dto.PricePerKg = 1_000_000m;
        Assert.False(_create.Validate(dto).IsValid);
    }

    [Fact]
    public void FutureDate_IsInvalid()
    {
        var dto = Create(Apiary, 1m, HiveProductType.Wax);
        dto.Date = DateTime.UtcNow.AddDays(3);
        Assert.False(_create.Validate(dto).IsValid);
    }

    [Fact]
    public void Update_AppliesTheSameOneLevelRule()
    {
        var both = new UpdateHarvestDto
        {
            Date = DateTime.UtcNow, ProductType = HiveProductType.Wax, BulkKg = 1m,
            Entries = [new CreateHarvestEntryDto { BeehiveId = 5, QuantityKg = 1m }],
        };
        var neither = new UpdateHarvestDto { Date = DateTime.UtcNow, ProductType = HiveProductType.Wax };
        var honeyWithoutType = new UpdateHarvestDto { Date = DateTime.UtcNow, ProductType = HiveProductType.Honey, BulkKg = 5m };

        Assert.False(_update.Validate(both).IsValid);
        Assert.False(_update.Validate(neither).IsValid);
        Assert.False(_update.Validate(honeyWithoutType).IsValid);
    }
}
