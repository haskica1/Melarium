namespace Melarium.Application.Common.Exceptions;

/// <summary>
/// Thrown when an upstream AI call could not be completed for a reason that is not the caller's
/// fault — Groq rate-limited us, was down, or did not answer in time. Maps to HTTP 503, which is
/// the honest signal: nothing about the request was wrong and trying again later is the fix.
/// Distinct from <see cref="BusinessRuleException"/> (422), which means the model answered but the
/// answer was unusable. Message is Bosnian, user-facing.
/// </summary>
public class AiUnavailableException : Exception
{
    public AiUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
