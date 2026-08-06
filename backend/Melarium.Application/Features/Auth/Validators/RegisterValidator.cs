using Melarium.Application.Common.Validation;
using Melarium.Application.Features.Auth.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Auth.Validators;

public class RegisterValidator : AbstractValidator<RegisterDto>
{
    public RegisterValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Phone).Phone();

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters.");

        RuleFor(x => x.OrganizationName)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(200).WithMessage("Organization name must not exceed 200 characters.");

        RuleFor(x => x.OrganizationDescription)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
            .When(x => x.OrganizationDescription is not null);

        // Length only. An unknown or malformed referral code is ignored later, never rejected here —
        // a bad ?ref= in a shared link must not stop someone from signing up (SPEC-15).
        RuleFor(x => x.ReferralCode)
            .MaximumLength(64).WithMessage("Referral code must not exceed 64 characters.")
            .When(x => x.ReferralCode is not null);
    }
}
