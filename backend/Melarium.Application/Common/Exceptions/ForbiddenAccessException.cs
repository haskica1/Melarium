namespace Melarium.Application.Common.Exceptions;

/// <summary>
/// Thrown when the caller is authenticated but not permitted to access the target resource.
/// Mapped to HTTP 403 Forbidden by <c>GlobalExceptionMiddleware</c>.
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message = "You do not have permission to access this resource.")
        : base(message) { }
}
