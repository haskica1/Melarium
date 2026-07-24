using Melarium.Application.Features.Inspections.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Inspections.Validators;

public class UpdateInspectionValidator : AbstractValidator<UpdateInspectionDto>
{
    public UpdateInspectionValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Inspection date cannot be in the future.");

        RuleFor(x => x.Temperature)
            .InclusiveBetween(-50, 60).WithMessage("Temperature must be between -50 and 60 °C.")
            .When(x => x.Temperature.HasValue);

        RuleFor(x => x.HoneyLevel).IsInEnum().WithMessage("Invalid honey level.");
        RuleFor(x => x.BeehiveId).GreaterThan(0).WithMessage("A valid beehive must be specified.");
    }
}
