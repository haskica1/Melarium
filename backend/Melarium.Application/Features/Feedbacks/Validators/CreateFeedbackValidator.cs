using FluentValidation;
using Melarium.Application.Features.Feedbacks.DTOs;

namespace Melarium.Application.Features.Feedbacks.Validators;

public class CreateFeedbackValidator : AbstractValidator<CreateFeedbackDto>
{
    public CreateFeedbackValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Nepoznata vrsta povratne informacije.");

        RuleFor(x => x.Severity)
            .IsInEnum().WithMessage("Nepoznat nivo ozbiljnosti.")
            .When(x => x.Severity.HasValue);

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Naslov je obavezan.")
            .MinimumLength(3).WithMessage("Naslov mora imati najmanje 3 znaka.")
            .MaximumLength(150).WithMessage("Naslov može imati najviše 150 znakova.");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Opis je obavezan.")
            .MinimumLength(10).WithMessage("Opišite malo detaljnije — najmanje 10 znakova.")
            .MaximumLength(2000).WithMessage("Opis može imati najviše 2000 znakova.");

        RuleFor(x => x.PageContext)
            .MaximumLength(300).WithMessage("Podatak o stranici je predugačak.");

        RuleFor(x => x.UserAgent)
            .MaximumLength(300).WithMessage("Podatak o pregledniku je predugačak.");
    }
}
