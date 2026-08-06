using Melarium.Application.Features.Diets.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Diets.Validators;

public class CompleteFeedingEntryValidator : AbstractValidator<CompleteFeedingEntryDto>
{
    public CompleteFeedingEntryValidator()
    {
        // No NotEmpty: ticking a round without a note is the normal case.
        RuleFor(x => x.Note)
            .MaximumLength(300).WithMessage("Napomena ne može biti duža od 300 znakova.");
    }
}
