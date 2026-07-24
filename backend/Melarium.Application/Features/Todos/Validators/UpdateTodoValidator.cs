using Melarium.Application.Features.Todos.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Todos.Validators;

public class UpdateTodoValidator : AbstractValidator<UpdateTodoDto>
{
    public UpdateTodoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes must not exceed 1000 characters.")
            .When(x => x.Notes is not null);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Invalid priority value.");

        RuleFor(x => x.AssignedToId)
            .GreaterThan(0).WithMessage("Invalid assignee.")
            .When(x => x.AssignedToId.HasValue);
    }
}
