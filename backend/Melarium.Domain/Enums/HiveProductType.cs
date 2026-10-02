namespace Melarium.Domain.Enums;

/// <summary>
/// What a <c>Harvest</c> collected (SPEC-30): honey — the original vrcanje — or another bee product.
/// English member names follow the codebase convention; Bosnian display labels live in <c>BsLabels</c>.
/// Quantities are always stored in kg: that propolis, royal jelly and venom are shown in grams is the
/// client's concern, not the model's.
///
/// <para>
/// Comb honey is deliberately its own product and <b>not</b> honey: it is not extracted, and Asim chose
/// to keep it out of the honey yield. <see cref="HarvestKind.Honey"/> means <see cref="Honey"/> alone.
/// </para>
/// </summary>
public enum HiveProductType
{
    Honey      = 1,  // Med (vrcani) — every harvest recorded before SPEC-30
    CombHoney  = 2,  // Med u saću
    Wax        = 3,  // Vosak
    Propolis   = 4,  // Propolis
    Pollen     = 5,  // Polen
    RoyalJelly = 6,  // Matična mliječ
    BeeBread   = 7,  // Perga
    BeeVenom   = 8,  // Apitoksin (pčelinji otrov)
    Other      = 99, // Ostalo
}
