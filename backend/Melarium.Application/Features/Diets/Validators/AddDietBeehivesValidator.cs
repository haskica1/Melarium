using Melarium.Application.Features.Diets.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Diets.Validators;

public class AddDietBeehivesValidator : AbstractValidator<AddDietBeehivesDto>
{
    public AddDietBeehivesValidator()
    {
        RuleFor(x => x.BeehiveIds)
            .NotEmpty().WithMessage("Odaberite bar jednu košnicu.");

        RuleFor(x => x.BeehiveIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Ista košnica je odabrana više puta.")
            .When(x => x.BeehiveIds.Count > 0);

        RuleFor(x => x.BeehiveIds)
            .Must(ids => ids.All(id => id > 0))
            .WithMessage("Neispravan identifikator košnice.")
            .When(x => x.BeehiveIds.Count > 0);
    }
}
