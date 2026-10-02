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
            .InclusiveBetween(HarvestLimits.MinKg, HarvestLimits.MaxEntryKg)
            .WithMessage("Količina po košnici mora biti veća od 0 i najviše 200 kg.");

        RuleFor(x => x.FramesExtracted)
            .InclusiveBetween(0, HarvestLimits.MaxFrames).WithMessage("Broj okvira mora biti između 0 i 200.")
            .When(x => x.FramesExtracted.HasValue);
    }
}
