using Melarium.Application.Common.Email;

namespace Melarium.Application.Common.Models;

/// <summary>
/// An email waiting to be delivered by the background email worker. <see cref="Content"/> says what
/// the mail contains; the worker renders it into the one Melarium template (ADR-048).
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
    EmailContent Content,
    string? ToEmail = null,
    string? ToName = null)
{
    /// <summary>Mail to a user account — the worker looks the address up when it dequeues.</summary>
    public static QueuedEmail ForUser(int userId, EmailContent content) => new(userId, content);

    /// <summary>Mail to an explicit address that may not belong to any account.</summary>
    public static QueuedEmail ForAddress(string toEmail, string toName, EmailContent content) =>
        new(null, content, toEmail, toName);
}
