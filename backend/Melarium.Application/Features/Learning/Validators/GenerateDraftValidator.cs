using Melarium.Application.Features.Learning.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Learning.Validators;

public class GenerateDraftValidator : AbstractValidator<GenerateDraftDto>
{
    public GenerateDraftValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Naslov je obavezan.")
            .MaximumLength(150).WithMessage("Naslov može imati najviše 150 znakova.");

        RuleFor(x => x.Outline)
            .MaximumLength(2000).WithMessage("Smjernice mogu imati najviše 2000 znakova.");
    }
}
