namespace Melarium.Application.Common.Exceptions;

/// <summary>
/// Thrown when authentication fails or a credential (e.g. a refresh token) is invalid, expired,
/// or revoked. Mapped to HTTP 401 Unauthorized by <c>GlobalExceptionMiddleware</c>.
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Authentication is required or has failed.")
        : base(message) { }
}
