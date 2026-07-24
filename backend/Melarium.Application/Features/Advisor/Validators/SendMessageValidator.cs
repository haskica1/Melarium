using Melarium.Application.Features.Advisor.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Advisor.Validators;

public class SendMessageValidator : AbstractValidator<SendMessageDto>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Poruka je obavezna.")
            .MaximumLength(4000).WithMessage("Poruka ne smije prelaziti 4000 znakova.");
    }
}
