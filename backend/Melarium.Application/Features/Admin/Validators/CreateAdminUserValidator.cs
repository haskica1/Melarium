using Melarium.Application.Common.Validation;
using Melarium.Application.Features.Admin.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Admin.Validators;

public class CreateAdminUserValidator : AbstractValidator<CreateAdminUserDto>
{
    public CreateAdminUserValidator()
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

        RuleFor(x => x.Password).Password();

        // Role/organisation/apiary consistency stays in AdminService (ValidateRoleOrgApiaryConsistency)
        // — it needs database lookups this validator has no access to.
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.");
    }
}
