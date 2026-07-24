using Melarium.Application.Features.Pastures.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Pastures.Validators;

public class CreateApiaryMoveValidator : AbstractValidator<CreateApiaryMoveDto>
{
    public CreateApiaryMoveValidator()
    {
        RuleFor(x => x.ToPastureId)
            .GreaterThan(0).WithMessage("Odaberite pašnjak.");

        // +1 day tolerance for evening entries across timezones (Treatments precedent).
        RuleFor(x => x.MovedAt)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1))
            .WithMessage("Datum selidbe ne može biti u budućnosti.");

        RuleFor(x => x.CertificateNumber)
            .MaximumLength(50).WithMessage("Broj svjedodžbe može imati najviše 50 znakova.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Napomena može imati najviše 500 znakova.");
    }
}
