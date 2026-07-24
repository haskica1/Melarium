using Melarium.Application.Features.Treatments.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Treatments.Validators;

public class TreatmentEntryValidator : AbstractValidator<CreateTreatmentEntryDto>
{
    public TreatmentEntryValidator()
    {
        RuleFor(x => x.BeehiveId)
            .GreaterThan(0).WithMessage("Košnica je obavezna.");

        RuleFor(x => x.DoseNote)
            .MaximumLength(100).WithMessage("Napomena o dozi ne smije prelaziti 100 znakova.")
            .When(x => x.DoseNote is not null);
    }
}
