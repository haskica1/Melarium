namespace Melarium.Application.Features.Auth.DTOs;

/// <param name="Identifier">Email address or phone number — either one signs the user in.</param>
/// <param name="Email">
/// Legacy name for <paramref name="Identifier"/>. Kept because the frontend is an installable PWA:
/// a client cached before this change still posts <c>email</c>, and dropping it would lock those
/// users out until their service worker updated.
/// </param>
public record LoginDto(string? Identifier, string Password, string? Email = null)
{
    /// <summary>What the user actually typed, whichever field carried it.</summary>
    public string LoginName =>
        string.IsNullOrWhiteSpace(Identifier) ? Email ?? string.Empty : Identifier;
}
