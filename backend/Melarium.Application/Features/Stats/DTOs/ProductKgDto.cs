using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Stats.DTOs;

/// <summary>One product type's kg. The enum travels with the label so a client sorts and translates by it.</summary>
public record ProductKgDto(HiveProductType ProductType, string Name, decimal Kg);
