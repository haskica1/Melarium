using Melarium.Application.Features.Harvests.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Harvests.Validators;

public class CreateHarvestValidator : AbstractValidator<CreateHarvestDto>
{
    public CreateHarvestValidator()
    {
        RuleFor(x => x.ApiaryId)
            .GreaterThan(0).WithMessage("Neispravan pčelinjak.")
            .When(x => x.ApiaryId.HasValue);

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Datum je obavezan.")
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1)).WithMessage("Datum ne može biti u budućnosti.");

        RuleFor(x => x.ProductType)
            .IsInEnum().WithMessage("Neispravna vrsta proizvoda.")
            .When(x => x.ProductType.HasValue);

        RuleFor(x => x.HoneyType)
            .NotNull().WithMessage("Vrsta meda je obavezna.")
            .IsInEnum().WithMessage("Neispravna vrsta meda.")
            .When(x => HarvestLimits.IsHoney(x.ProductType));

        RuleFor(x => x.PricePerKg)
            .InclusiveBetween(0m, HarvestLimits.MaxPricePerKg)
            .WithMessage("Cijena mora biti 0 ili veća, a najviše 999.999 KM/kg.")
            .When(x => x.PricePerKg.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(HarvestLimits.MaxNotesLength).WithMessage("Napomena ne smije prelaziti 500 znakova.")
            .When(x => x.Notes is not null);

        // The quantity is recorded at exactly one level: per hive, or one figure.
        RuleFor(x => x.BulkKg)
            .NotNull().WithMessage("Unesite količinu — po košnicama ili ukupno.")
            .When(x => x.Entries.Count == 0);

        RuleFor(x => x.BulkKg)
            .Null().WithMessage("Unesite količinu ili po košnicama ili ukupno, ne oboje.")
            .When(x => x.Entries.Count > 0);

        RuleFor(x => x.BulkKg)
            .InclusiveBetween(HarvestLimits.MinKg, HarvestLimits.MaxBulkKg)
            .WithMessage("Količina mora biti veća od 0 i najviše 100.000 kg.")
            .When(x => x.BulkKg.HasValue);

        // A record of the whole organization belongs to no apiary, so there are no hives to split it over.
        RuleFor(x => x.Entries)
            .Empty().WithMessage("Zapis za cijelu organizaciju nema raspodjelu po košnicama — unesite ukupnu količinu.")
            .When(x => x.ApiaryId is null);

        RuleFor(x => x.Entries)
            .Must(entries => entries.Select(e => e.BeehiveId).Distinct().Count() == entries.Count)
            .WithMessage("Ista košnica se ne može navesti više puta.")
            .When(x => x.Entries.Count > 0);

        RuleForEach(x => x.Entries).SetValidator(new HarvestEntryValidator());
    }
}
