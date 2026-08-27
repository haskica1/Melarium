using FluentValidation;
using Melarium.Application.Features.BeehiveMerges.DTOs;

namespace Melarium.Application.Features.BeehiveMerges.Validators;

public class CreateBeehiveMergeValidator : AbstractValidator<CreateBeehiveMergeDto>
{
    public CreateBeehiveMergeValidator()
    {
        RuleFor(x => x.SourceBeehiveId)
            .GreaterThan(0).WithMessage("Košnica koja se pripaja je obavezna.");

        RuleFor(x => x.TargetBeehiveId)
            .GreaterThan(0).WithMessage("Prijemna košnica je obavezna.")
            .NotEqual(x => x.SourceBeehiveId)
            .WithMessage("Košnica se ne može sastaviti sama sa sobom.");

        RuleFor(x => x.MergedAt)
            .NotEmpty().WithMessage("Datum sastavljanja je obavezan.")
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1))
            .WithMessage("Datum sastavljanja ne može biti u budućnosti.");

        RuleFor(x => x.Reason).IsInEnum().WithMessage("Neispravan razlog sastavljanja.");
        RuleFor(x => x.Method).IsInEnum().WithMessage("Neispravna metoda sastavljanja.");
        RuleFor(x => x.QueenOutcome).IsInEnum().WithMessage("Neispravan odabir matice.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Napomena ne smije prelaziti 1000 znakova.")
            .When(x => x.Notes is not null);
    }
}
