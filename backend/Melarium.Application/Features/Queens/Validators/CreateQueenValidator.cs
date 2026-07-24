using Melarium.Application.Features.Queens.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Queens.Validators;

public class CreateQueenValidator : AbstractValidator<CreateQueenDto>
{
    public CreateQueenValidator()
    {
        RuleFor(x => x.Year)
            .Must(y => y >= 2000 && y <= DateTime.UtcNow.Year)
            .WithMessage("Queen year must be between 2000 and the current year.");

        RuleFor(x => x.MarkColor)
            .IsInEnum().WithMessage("Invalid mark color.")
            .When(x => x.MarkColor.HasValue);

        RuleFor(x => x.Origin)
            .IsInEnum().WithMessage("Invalid queen origin.");

        RuleFor(x => x.IntroducedDate)
            .NotEmpty().WithMessage("Introduced date is required.")
            .Must(d => d <= DateTime.UtcNow.AddDays(1))
            .WithMessage("Introduced date cannot be in the future.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes must not exceed 500 characters.")
            .When(x => x.Notes is not null);
    }
}
