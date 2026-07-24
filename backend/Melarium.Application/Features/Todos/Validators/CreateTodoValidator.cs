using Melarium.Application.Features.Todos.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Todos.Validators;

public class CreateTodoValidator : AbstractValidator<CreateTodoDto>
{
    public CreateTodoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes must not exceed 1000 characters.")
            .When(x => x.Notes is not null);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Invalid priority value.");

        RuleFor(x => x)
            .Must(x => x.ApiaryId.HasValue ^ x.BeehiveId.HasValue)
            .WithName("Target")
            .WithMessage("A todo must belong to exactly one apiary or one beehive.");

        RuleFor(x => x.ApiaryId)
            .GreaterThan(0).WithMessage("A valid apiary must be specified.")
            .When(x => x.ApiaryId.HasValue);

        RuleFor(x => x.BeehiveId)
            .GreaterThan(0).WithMessage("A valid beehive must be specified.")
            .When(x => x.BeehiveId.HasValue);

        RuleFor(x => x.AssignedToId)
            .GreaterThan(0).WithMessage("Invalid assignee.")
            .When(x => x.AssignedToId.HasValue);
    }
}
