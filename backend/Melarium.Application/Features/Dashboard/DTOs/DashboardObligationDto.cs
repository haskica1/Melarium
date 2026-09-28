namespace Melarium.Application.Features.Dashboard.DTOs;

/// <param name="Kind">The <c>ObligationKind</c> name.</param>
public record DashboardObligationDto(DateOnly Date, string Kind, string Title, string LinkPath);
