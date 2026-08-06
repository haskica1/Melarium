namespace Melarium.Domain.Enums;

/// <summary>
/// Unit for <see cref="Entities.Diet.AmountPerHive"/>. Deliberately a small closed set rather than
/// free text: the number has to stay machine-readable so feeding consumption (and from it, cost)
/// can be computed. Anything the number cannot express — "1:1", "pola pogače" — belongs in
/// <see cref="Entities.Diet.AmountNote"/>.
/// </summary>
public enum FeedingAmountUnit
{
    Litre      = 1,
    Millilitre = 2,
    Kilogram   = 3,
    Gram       = 4,
}
