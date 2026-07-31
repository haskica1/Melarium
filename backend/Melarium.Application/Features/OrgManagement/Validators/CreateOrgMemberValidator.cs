using Melarium.Application.Common.Validation;
using Melarium.Application.Features.OrgManagement.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.OrgManagement.Validators;

public class CreateOrgMemberValidator : AbstractValidator<CreateOrgMemberDto>
{
    public CreateOrgMemberValidator()
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

        RuleFor(x => x.Password).Password();

        // Role/apiary/beehive consistency stays in OrgManagementService — it needs the
        // caller's organisation to validate against.
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.");
    }
}
