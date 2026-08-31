namespace Melarium.Application.Features.Profile.DTOs;

/// <summary>
/// Confirmation payload for deleting your own account.
/// </summary>
/// <param name="Password">
/// The caller's current password. Re-entered rather than trusted from the session: an unlocked
/// phone left on a table must not be two taps away from destroying the account.
/// </param>
/// <param name="OrganizationNameConfirmation">
/// Required only when the deletion also removes the organization (the caller is its last member) —
/// the organization's exact name, typed by hand. Ignored in every other case.
/// </param>
public record DeleteAccountDto(string Password, string? OrganizationNameConfirmation);
