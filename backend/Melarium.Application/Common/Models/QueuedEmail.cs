namespace Melarium.Application.Common.Models;

/// <summary>
/// An email waiting to be delivered by the background email worker.
/// <c>ActionUrl</c>/<c>ActionLabel</c> render a call-to-action button — used by password reset and
/// email verification, which are useless without a link. Both are optional so plain notification
/// emails are unaffected.
/// </summary>
/// <remarks>
/// Two addressing modes. Notification mail targets a <see cref="UserId"/> and the worker resolves the
/// address from the account, so a deleted user is skipped instead of mailed. Operator mail (new
/// feedback, SPEC-13) targets <see cref="ToEmail"/> directly, because the destination is a configured
/// address that need not correspond to any account. Use the <see cref="ForUser"/> /
/// <see cref="ForAddress"/> factories rather than the constructor so the intent is visible at the
/// call site.
/// </remarks>
public sealed record QueuedEmail(
    int? UserId,
    string Title,
    string Message,
    string? ActionUrl = null,
    string? ActionLabel = null,
    string? ToEmail = null,
    string? ToName = null)
{
    /// <summary>Mail to a user account — the worker looks the address up when it dequeues.</summary>
    public static QueuedEmail ForUser(
        int userId, string title, string message, string? actionUrl = null, string? actionLabel = null) =>
        new(userId, title, message, actionUrl, actionLabel);

    /// <summary>Mail to an explicit address that may not belong to any account.</summary>
    public static QueuedEmail ForAddress(
        string toEmail, string toName, string title, string message,
        string? actionUrl = null, string? actionLabel = null) =>
        new(null, title, message, actionUrl, actionLabel, toEmail, toName);
}
