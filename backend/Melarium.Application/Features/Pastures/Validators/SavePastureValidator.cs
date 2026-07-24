using Melarium.Application.Features.Pastures.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Pastures.Validators;

public class SavePastureValidator : AbstractValidator<SavePastureDto>
{
    public SavePastureValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Naziv pašnjaka je obavezan.")
            .MaximumLength(100).WithMessage("Naziv može imati najviše 100 znakova.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).WithMessage("Geografska širina mora biti između -90 i 90.")
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).WithMessage("Geografska dužina mora biti između -180 i 180.")
            .When(x => x.Longitude.HasValue);

        RuleFor(x => x)
            .Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithMessage("Unesite obje koordinate ili nijednu.")
            .WithName("location");

        RuleFor(x => x.Address).MaximumLength(200).WithMessage("Adresa može imati najviše 200 znakova.");
        RuleFor(x => x.FloraNotes).MaximumLength(300).WithMessage("Opis flore može imati najviše 300 znakova.");
        RuleFor(x => x.Notes).MaximumLength(500).WithMessage("Napomena može imati najviše 500 znakova.");
    }
}
