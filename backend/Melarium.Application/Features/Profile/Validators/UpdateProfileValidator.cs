using Melarium.Application.Common.Validation;
using Melarium.Application.Features.Profile.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Profile.Validators;

public class UpdateProfileValidator : AbstractValidator<UpdateProfileDto>
{
    public UpdateProfileValidator()
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

        // Password change is optional — the policy applies only when a new one is supplied.
        // ProfileService still verifies CurrentPassword before applying it.
        RuleFor(x => x.NewPassword!)
            .Password()
            .When(x => !string.IsNullOrWhiteSpace(x.NewPassword));

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required to set a new password.")
            .When(x => !string.IsNullOrWhiteSpace(x.NewPassword));
    }
}
