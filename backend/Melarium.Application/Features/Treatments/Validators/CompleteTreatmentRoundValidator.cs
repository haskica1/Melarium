using Melarium.Application.Features.Treatments.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Treatments.Validators;

public class CompleteTreatmentRoundValidator : AbstractValidator<CompleteTreatmentRoundDto>
{
    public CompleteTreatmentRoundValidator()
    {
        // No NotEmpty: ticking a round without a note is the normal case.
        RuleFor(x => x.Note)
            .MaximumLength(300).WithMessage("Napomena ne može biti duža od 300 znakova.");
    }
}
