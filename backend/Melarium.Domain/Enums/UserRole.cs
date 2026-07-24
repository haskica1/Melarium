namespace Melarium.Domain.Enums;

/// <summary>
/// Access tiers, from narrowest to broadest scope. Numeric values are persisted, so they must
/// remain stable across renames.
/// </summary>
public enum UserRole
{
    /// <summary>Manages a single assigned apiary and its beehives.</summary>
    ApiaryAdmin = 1,

    /// <summary>Platform-wide administrator — manages organizations and users.</summary>
    SystemAdmin = 2,

    /// <summary>Manages an entire organization.</summary>
    OrganizationAdmin = 3,

    /// <summary>Field beekeeper, scoped to explicitly assigned beehives.</summary>
    Beekeeper = 4,
}
