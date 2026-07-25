using FluentValidation;

namespace Melarium.Application.Common.Validation;

/// <summary>
/// The single password policy for the whole platform. Every entry point that sets a password
/// (self-registration, admin-created users, organisation members, profile change) applies this
/// rule, so the policy cannot drift between them — it previously existed only on registration.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(MinLength).WithMessage($"Password must be at least {MinLength} characters long.")
            .MaximumLength(MaxLength).WithMessage($"Password must not exceed {MaxLength} characters.");
}
