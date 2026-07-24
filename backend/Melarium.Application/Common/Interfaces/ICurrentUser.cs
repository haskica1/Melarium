using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the currently authenticated caller, resolved from the request's JWT claims.
/// Implemented in the API layer; consumed by Application services so authorization logic never
/// touches <c>HttpContext</c> or parses raw claim strings.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The caller's user id, or null when unauthenticated.</summary>
    int? UserId { get; }

    /// <summary>The caller's role, or null when unauthenticated / unrecognized.</summary>
    UserRole? Role { get; }

    /// <summary>The organization the caller belongs to, or null when not org-scoped.</summary>
    int? OrganizationId { get; }

    /// <summary>The apiary an ApiaryAdmin is scoped to, or null for other roles.</summary>
    int? ApiaryId { get; }

    /// <summary>True when the request carries a valid authenticated identity.</summary>
    bool IsAuthenticated { get; }
}
