using Melarium.Application.Common.Interfaces;
using Melarium.Domain.Enums;

namespace Melarium.Application.Tests;

/// <summary>Init-only <see cref="ICurrentUser"/> stand-in for authorization tests.</summary>
public sealed class TestCurrentUser : ICurrentUser
{
    public int? UserId { get; init; }
    public UserRole? Role { get; init; }
    public int? OrganizationId { get; init; }
    public int? ApiaryId { get; init; }
    public bool IsAuthenticated => UserId.HasValue;
}
