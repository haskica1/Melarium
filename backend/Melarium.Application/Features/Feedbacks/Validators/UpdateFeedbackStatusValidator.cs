using FluentValidation;
using Melarium.Application.Features.Feedbacks.DTOs;

namespace Melarium.Application.Features.Feedbacks.Validators;

public class UpdateFeedbackStatusValidator : AbstractValidator<UpdateFeedbackStatusDto>
{
    public UpdateFeedbackStatusValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Nepoznat status.");

        RuleFor(x => x.AdminResponse)
            .MaximumLength(1000).WithMessage("Odgovor može imati najviše 1000 znakova.");
    }
}
