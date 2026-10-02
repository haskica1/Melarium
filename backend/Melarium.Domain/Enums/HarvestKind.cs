namespace Melarium.Domain.Enums;

/// <summary>
/// Which side of the honey / other-products line a harvest query reads (SPEC-30).
///
/// <para>
/// Honey and the other products share one table but are never summed together — a kilo of wax is not
/// honey, and 200 g of royal jelly beside 20 kg of wax makes no total. Every aggregate therefore has to
/// name its side: the repository methods that feed sums take this as a required argument, so a new
/// report cannot quietly add wax to "kg meda".
/// </para>
/// </summary>
public enum HarvestKind
{
    /// <summary><see cref="HiveProductType.Honey"/> only — the honey yield since SPEC-02.</summary>
    Honey = 1,

    /// <summary>Everything except honey, comb honey included.</summary>
    OtherProducts = 2,

    /// <summary>Lists only — never an input to a sum of kilograms.</summary>
    All = 3,
}
