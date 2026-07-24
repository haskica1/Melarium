using Melarium.Application.Features.Diets.DTOs;
using Melarium.Domain.Enums;
using FluentValidation;

namespace Melarium.Application.Features.Diets.Validators;

public class UpdateDietValidator : AbstractValidator<UpdateDietDto>
{
    public UpdateDietValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Diet name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        RuleFor(x => x.Reason)
            .IsInEnum().WithMessage("Invalid diet reason.");

        RuleFor(x => x.CustomReason)
            .NotEmpty().WithMessage("Custom reason text is required when reason is Custom.")
            .MaximumLength(500)
            .When(x => x.Reason == DietReason.Custom);

        RuleFor(x => x.DurationDays)
            .GreaterThan(0).WithMessage("Duration must be at least 1 day.");

        RuleFor(x => x.FrequencyDays)
            .GreaterThan(0).WithMessage("Frequency must be at least 1 day.")
            .LessThanOrEqualTo(x => x.DurationDays).WithMessage("Frequency cannot exceed duration.");

        RuleFor(x => x.FoodType)
            .IsInEnum().WithMessage("Invalid food type.");

        RuleFor(x => x.CustomFoodType)
            .NotEmpty().WithMessage("Custom food type text is required when food type is Custom.")
            .MaximumLength(200)
            .When(x => x.FoodType == FoodType.Custom);
    }
}
