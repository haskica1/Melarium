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
            .GreaterThan(0).WithMessage("Duration must be at least 1 day.")
            .LessThanOrEqualTo(365).WithMessage("Duration must not exceed 365 days.");

        RuleFor(x => x.FrequencyDays)
            .GreaterThan(0).WithMessage("Frequency must be at least 1 day.")
            .LessThanOrEqualTo(x => x.DurationDays).WithMessage("Frequency cannot exceed duration.");

        RuleFor(x => x.FoodType)
            .IsInEnum().WithMessage("Invalid food type.");

        RuleFor(x => x.CustomFoodType)
            .NotEmpty().WithMessage("Custom food type text is required when food type is Custom.")
            .MaximumLength(200)
            .When(x => x.FoodType == FoodType.Custom);

        RuleFor(x => x.AmountPerHive)
            .GreaterThan(0).WithMessage("Količina po košnici mora biti veća od 0.")
            .LessThanOrEqualTo(100).WithMessage("Količina po košnici ne može biti veća od 100.")
            .When(x => x.AmountPerHive.HasValue);

        RuleFor(x => x.AmountUnit)
            .NotNull().WithMessage("Odaberite jedinicu za količinu.")
            .When(x => x.AmountPerHive.HasValue);

        RuleFor(x => x.AmountUnit)
            .IsInEnum().WithMessage("Neispravna jedinica količine.")
            .When(x => x.AmountUnit.HasValue);

        RuleFor(x => x.AmountNote)
            .MaximumLength(100).WithMessage("Napomena o količini ne može biti duža od 100 znakova.");
    }
}
