using Melarium.Application.Features.Diets.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Diets.Validators;

public class CopyDietValidator : AbstractValidator<CopyDietDto>
{
    public CopyDietValidator()
    {
        RuleFor(x => x.TargetBeehiveIds)
            .NotEmpty().WithMessage("Odaberite bar jednu košnicu za kopiranje.");

        RuleForEach(x => x.TargetBeehiveIds)
            .GreaterThan(0).WithMessage("Nevažeći identifikator košnice.");
    }
}
