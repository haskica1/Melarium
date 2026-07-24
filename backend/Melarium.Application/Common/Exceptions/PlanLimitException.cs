namespace Melarium.Application.Common.Exceptions;

/// <summary>
/// Thrown when an action exceeds the organization's subscription plan (SPEC-09).
/// Maps to HTTP 402 with <c>code: "plan-limit"</c> — distinct from 403 so the frontend
/// renders an upgrade prompt instead of "nemate pravo". Message is Bosnian, user-facing.
/// </summary>
public class PlanLimitException : Exception
{
    public PlanLimitException(string message) : base(message) { }
}
