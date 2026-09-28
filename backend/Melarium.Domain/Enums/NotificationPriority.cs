namespace Melarium.Domain.Enums;

/// <summary>
/// How loudly a notification is delivered (SPEC-29): <see cref="Critical"/> goes in-app and by e-mail
/// at once, <see cref="Normal"/> goes in-app and into the one morning e-mail, <see cref="Info"/> stays
/// in the app.
/// </summary>
/// <remarks>
/// <see cref="Normal"/> is deliberately <c>0</c>: every notification written before priorities existed
/// reads as Normal without a backfill, and a code path that never sets a priority cannot accidentally
/// produce a Critical e-mail.
/// </remarks>
public enum NotificationPriority
{
    Normal   = 0,
    Critical = 1,
    Info     = 2,
}
