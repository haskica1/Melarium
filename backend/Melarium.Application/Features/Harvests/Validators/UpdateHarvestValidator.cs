using Melarium.Application.Features.Harvests.DTOs;
using Melarium.Domain.Enums;
using FluentValidation;

namespace Melarium.Application.Features.Harvests.Validators;

/// <summary>
/// Same rules as create, minus the apiary. Two checks depend on the stored record and live in the
/// service: a record of the whole organization cannot be split per hive, and a missing product means
/// "keep the record's own" — so honey type is enforced here only when honey is named explicitly.
/// </summary>
public class UpdateHarvestValidator : AbstractValidator<UpdateHarvestDto>
{
    public UpdateHarvestValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Datum je obavezan.")
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1)).WithMessage("Datum ne može biti u budućnosti.");

        RuleFor(x => x.ProductType)
            .IsInEnum().WithMessage("Neispravna vrsta proizvoda.")
            .When(x => x.ProductType.HasValue);

        RuleFor(x => x.HoneyType)
            .NotNull().WithMessage("Vrsta meda je obavezna.")
            .IsInEnum().WithMessage("Neispravna vrsta meda.")
            .When(x => x.ProductType == HiveProductType.Honey);

        RuleFor(x => x.HoneyType)
            .IsInEnum().WithMessage("Neispravna vrsta meda.")
            .When(x => x.HoneyType.HasValue);

        RuleFor(x => x.PricePerKg)
            .InclusiveBetween(0m, HarvestLimits.MaxPricePerKg)
            .WithMessage("Cijena mora biti 0 ili veća, a najviše 999.999 KM/kg.")
            .When(x => x.PricePerKg.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(HarvestLimits.MaxNotesLength).WithMessage("Napomena ne smije prelaziti 500 znakova.")
            .When(x => x.Notes is not null);

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

        RuleFor(x => x.Entries)
            .Must(entries => entries.Select(e => e.BeehiveId).Distinct().Count() == entries.Count)
            .WithMessage("Ista košnica se ne može navesti više puta.")
            .When(x => x.Entries.Count > 0);

        RuleForEach(x => x.Entries).SetValidator(new HarvestEntryValidator());
    }
}
