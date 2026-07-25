using Melarium.Application.Features.Admin.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Admin.Validators;

/// <summary>
/// The update payload carries no password (<see cref="UpdateAdminUserDto"/>) — password changes
/// go through the owner's profile — so this only bounds the free-text fields.
/// </summary>
public class UpdateAdminUserValidator : AbstractValidator<UpdateAdminUserDto>
{
    public UpdateAdminUserValidator()
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

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.");
    }
}
