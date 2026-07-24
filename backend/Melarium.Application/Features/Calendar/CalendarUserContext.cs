using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Calendar;

/// <summary>
/// The resolved identity a calendar aggregation runs for. Built from <c>ICurrentUser</c> on the
/// authenticated JSON path, or from a feed-token lookup on the anonymous ICS path — so the
/// aggregation never touches <c>ICurrentUser</c> and can run off the request thread (daily agenda worker).
/// </summary>
public sealed record CalendarUserContext(int UserId, UserRole Role, int? OrganizationId, int? ApiaryId);
