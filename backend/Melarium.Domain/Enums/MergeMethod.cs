namespace Melarium.Domain.Enums;

/// <summary>How the two colonies were physically united (SPEC-19 §0).</summary>
public enum MergeMethod
{
    /// <summary>Preko novinskog papira — the dominant method; bees chew through over a few days.</summary>
    Newspaper = 1,

    /// <summary>Direct union, with the scents masked (diluted rakija, basil water).</summary>
    Direct    = 2,

    Other     = 3,
}
