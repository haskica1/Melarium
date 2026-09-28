namespace Melarium.Domain.Enums;

/// <summary>
/// A user's e-mail preference (SPEC-29). Applies to every notification except the security ones
/// (password changed, new account, organization handed over), which are always mailed — an attacker
/// who took over the account must not be able to silence the one message that would tell its owner.
/// </summary>
public enum EmailNotificationMode
{
    /// <summary>Critical at once, alerts in the morning e-mail, human-triggered news (a todo, an assignment) at once.</summary>
    All          = 1,
    CriticalOnly = 2,
    /// <summary>No e-mail at all; Critical still appears in the app.</summary>
    Off          = 3,
}
