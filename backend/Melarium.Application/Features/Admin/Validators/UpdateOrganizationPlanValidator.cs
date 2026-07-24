using Melarium.Application.Features.Admin.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Admin.Validators;

public class UpdateOrganizationPlanValidator : AbstractValidator<UpdateOrganizationPlanDto>
{
    public UpdateOrganizationPlanValidator()
    {
        RuleFor(x => x.Plan).IsInEnum();
        RuleFor(x => x.PlanNotes).MaximumLength(300);
    }
}
