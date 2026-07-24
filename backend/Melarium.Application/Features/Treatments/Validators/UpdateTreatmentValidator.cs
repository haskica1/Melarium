using Melarium.Application.Features.Treatments.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Treatments.Validators;

public class UpdateTreatmentValidator : AbstractValidator<UpdateTreatmentDto>
{
    public UpdateTreatmentValidator()
    {
        RuleFor(x => x.Purpose).IsInEnum().WithMessage("Neispravna namjena tretmana.");
        RuleFor(x => x.ActiveSubstance).IsInEnum().WithMessage("Neispravna aktivna tvar.");
        RuleFor(x => x.Method).IsInEnum().WithMessage("Neispravan način primjene.");

        RuleFor(x => x.ProductName)
            .NotEmpty().WithMessage("Naziv preparata je obavezan.")
            .MaximumLength(100).WithMessage("Naziv preparata ne smije prelaziti 100 znakova.");

        RuleFor(x => x.DosePerHive)
            .NotEmpty().WithMessage("Doza po košnici je obavezna.")
            .MaximumLength(100).WithMessage("Doza ne smije prelaziti 100 znakova.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Datum početka je obavezan.")
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1)).WithMessage("Datum početka ne može biti u budućnosti.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("Datum završetka ne može biti prije početka.")
            .When(x => x.EndDate.HasValue);

        RuleFor(x => x.WithdrawalDays)
            .InclusiveBetween(0, 365).WithMessage("Karenca mora biti između 0 i 365 dana.");

        RuleFor(x => x.BatchNumber).MaximumLength(50).When(x => x.BatchNumber is not null);
        RuleFor(x => x.Supplier).MaximumLength(100).When(x => x.Supplier is not null);
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes is not null);

        RuleFor(x => x.Entries)
            .NotEmpty().WithMessage("Potrebna je barem jedna košnica.");

        RuleFor(x => x.Entries)
            .Must(e => e.Select(x => x.BeehiveId).Distinct().Count() == e.Count)
            .WithMessage("Ista košnica se ne može navesti više puta.")
            .When(x => x.Entries.Count > 0);

        RuleForEach(x => x.Entries).SetValidator(new TreatmentEntryValidator());
    }
}
