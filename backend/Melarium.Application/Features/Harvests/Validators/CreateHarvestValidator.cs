using Melarium.Application.Features.Harvests.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Harvests.Validators;

public class CreateHarvestValidator : AbstractValidator<CreateHarvestDto>
{
    public CreateHarvestValidator()
    {
        RuleFor(x => x.ApiaryId)
            .GreaterThan(0).WithMessage("Pčelinjak je obavezan.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Datum je obavezan.")
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1)).WithMessage("Datum ne može biti u budućnosti.");

        RuleFor(x => x.HoneyType)
            .IsInEnum().WithMessage("Neispravna vrsta meda.");

        RuleFor(x => x.PricePerKg)
            .GreaterThanOrEqualTo(0).WithMessage("Cijena po kg mora biti 0 ili veća.")
            .When(x => x.PricePerKg.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Napomena ne smije prelaziti 500 znakova.")
            .When(x => x.Notes is not null);

        RuleFor(x => x.Entries)
            .NotEmpty().WithMessage("Potrebna je barem jedna košnica u evidenciji vrcanja.");

        RuleFor(x => x.Entries)
            .Must(entries => entries.Select(e => e.BeehiveId).Distinct().Count() == entries.Count)
            .WithMessage("Ista košnica se ne može navesti više puta.")
            .When(x => x.Entries.Count > 0);

        RuleForEach(x => x.Entries).SetValidator(new HarvestEntryValidator());
    }
}
