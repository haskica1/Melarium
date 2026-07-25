namespace Melarium.Application.Common.Models;

/// <summary>
/// An email waiting to be delivered by the background email worker.
/// <c>ActionUrl</c>/<c>ActionLabel</c> render a call-to-action button — used by password reset and
/// email verification, which are useless without a link. Both are optional so plain notification
/// emails are unaffected.
/// </summary>
public sealed record QueuedEmail(
    int UserId,
    string Title,
    string Message,
    string? ActionUrl = null,
    string? ActionLabel = null);
