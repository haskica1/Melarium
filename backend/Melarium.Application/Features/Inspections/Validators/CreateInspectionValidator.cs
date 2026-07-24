using Melarium.Application.Features.Inspections.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Inspections.Validators;

public class CreateInspectionValidator : AbstractValidator<CreateInspectionDto>
{
    public CreateInspectionValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Inspection date is required.")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Inspection date cannot be in the future.");

        RuleFor(x => x.Temperature)
            .InclusiveBetween(-50, 60).WithMessage("Temperature must be between -50 and 60 °C.")
            .When(x => x.Temperature.HasValue);

        RuleFor(x => x.HoneyLevel)
            .IsInEnum().WithMessage("Invalid honey level.");

        RuleFor(x => x.BeehiveId)
            .GreaterThan(0).WithMessage("A valid beehive must be specified.");

        RuleFor(x => x.BroodStatus)
            .MaximumLength(500).WithMessage("Brood status must not exceed 500 characters.")
            .When(x => x.BroodStatus is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.")
            .When(x => x.Notes is not null);
    }
}
