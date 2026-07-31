using FluentValidation;

namespace Melarium.Application.Common.Validation;

/// <summary>
/// The single phone-number policy for the platform. Numbers are stored in one canonical form
/// (E.164, e.g. <c>+38761123456</c>) because <c>User.Phone</c> is both a unique key and a login
/// identifier: without canonicalisation "061 123 456", "+387 61 123 456" and "0038761123456"
/// would be three different accounts, and a user who signs up typing one form could not sign in
/// typing another.
/// </summary>
public static class PhoneRules
{
    /// <summary>Assumed for numbers typed in local form — the platform's market is BiH.</summary>
    private const string DefaultCountryCode = "387";

    /// <summary>'+' plus E.164's maximum of 15 digits.</summary>
    public const int MaxLength = 16;

    /// <summary>
    /// Converts anything a person might type into canonical E.164, or null if it cannot be
    /// one. Callers treat null as "not a usable number" — both the validator and the login
    /// lookup rely on that, so an unparseable input can never match a stored number.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim();

        // Spaces, dashes, slashes, dots and brackets are all common in how numbers get written.
        var digits = new string([.. trimmed.Where(char.IsAsciiDigit)]);
        if (digits.Length == 0) return null;

        var national = trimmed.StartsWith('+') || digits.StartsWith(DefaultCountryCode)
            // Already international: a leading '+', or bare digits opening with the country code —
            // no BiH national number can start with 387 (they start with 0, or 6/3/5 once dropped).
            ? digits
            : digits.StartsWith("00")
                ? digits[2..]
                : DefaultCountryCode + digits.TrimStart('0');

        var normalized = "+" + national;
        return IsValid(normalized) ? normalized : null;
    }

    /// <summary>E.164 shape: '+', a non-zero country digit, and 8–15 digits in total.</summary>
    private static bool IsValid(string candidate) =>
        candidate.Length is >= 9 and <= MaxLength && candidate[1] != '0';

    /// <summary>
    /// Raised wherever a number is already registered to another account. Shared so the four
    /// entry points that can set a phone (registration, admin create/update, org member create,
    /// profile) cannot word the same rule differently.
    /// </summary>
    public const string DuplicateMessage = "Korisnik s ovim brojem telefona već postoji.";

    private const string InvalidMessage = "Enter a valid phone number (e.g. 061 123 456).";

    /// <summary>For payloads where the number must be supplied — sign-up and user creation.</summary>
    public static IRuleBuilderOptions<T, string> Phone<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Phone number is required.")
            .Must(value => Normalize(value) is not null)
            .WithMessage(InvalidMessage);

    /// <summary>
    /// For update payloads, where a blank field means "leave the stored number alone" rather than
    /// "clear it" — a client that omits the field must never silently drop a login identifier.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> OptionalPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .Must(value => string.IsNullOrWhiteSpace(value) || Normalize(value) is not null)
            .WithMessage(InvalidMessage);
}
