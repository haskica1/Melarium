namespace Melarium.Application.Features.Diets.DTOs;

/// <summary>Total money attributed to a programme in one currency (SPEC-12 Phase E).</summary>
public class CostTotalDto
{
    public string Currency { get; set; } = string.Empty;
    public decimal Total { get; set; }
}
