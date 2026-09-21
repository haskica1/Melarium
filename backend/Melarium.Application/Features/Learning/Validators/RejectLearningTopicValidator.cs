using Melarium.Application.Features.Learning.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Learning.Validators;

public class RejectLearningTopicValidator : AbstractValidator<RejectLearningTopicDto>
{
    public RejectLearningTopicValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Razlog odbijanja je obavezan.")
            .MinimumLength(10).WithMessage("Razlog odbijanja mora imati najmanje 10 znakova.")
            .MaximumLength(500).WithMessage("Razlog odbijanja može imati najviše 500 znakova.");
    }
}
