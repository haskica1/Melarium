namespace Melarium.Application.Features.Stats.DTOs;

/// <summary>Name/value pair with a decimal value (e.g. kg totals) — the decimal counterpart of <see cref="NameValueDto"/>.</summary>
public record NameDecimalDto(string Name, decimal Value);
