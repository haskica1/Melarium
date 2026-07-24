using Melarium.Application.Features.Diets.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Diets.Validators;

public class CompleteEarlyValidator : AbstractValidator<CompleteEarlyDto>
{
    public CompleteEarlyValidator()
    {
        RuleFor(x => x.Comment)
            .NotEmpty().WithMessage("A comment is required when stopping a diet early.")
            .MaximumLength(1000).WithMessage("Comment must not exceed 1000 characters.");
    }
}
