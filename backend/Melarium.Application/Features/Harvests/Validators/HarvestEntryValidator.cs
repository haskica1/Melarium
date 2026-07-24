using Melarium.Application.Features.Harvests.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Harvests.Validators;

public class HarvestEntryValidator : AbstractValidator<CreateHarvestEntryDto>
{
    public HarvestEntryValidator()
    {
        RuleFor(x => x.BeehiveId)
            .GreaterThan(0).WithMessage("Košnica je obavezna.");

        RuleFor(x => x.QuantityKg)
            .InclusiveBetween(0.1m, 200m)
            .WithMessage("Količina po košnici mora biti između 0.1 i 200 kg.");

        RuleFor(x => x.FramesExtracted)
            .InclusiveBetween(0, 200).WithMessage("Broj okvira mora biti između 0 i 200.")
            .When(x => x.FramesExtracted.HasValue);
    }
}
