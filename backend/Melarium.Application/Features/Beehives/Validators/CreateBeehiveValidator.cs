using Melarium.Application.Features.Beehives.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Beehives.Validators;

public class CreateBeehiveValidator : AbstractValidator<CreateBeehiveDto>
{
    public CreateBeehiveValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Beehive name/label is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid beehive type.");

        RuleFor(x => x.Material)
            .IsInEnum().WithMessage("Invalid beehive material.");

        RuleFor(x => x.DateCreated)
            .NotEmpty().WithMessage("Date created is required.")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Date created cannot be in the future.");

        RuleFor(x => x.ApiaryId)
            .GreaterThan(0).WithMessage("A valid apiary must be specified.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.")
            .When(x => x.Notes is not null);

        RuleFor(x => x.LabelNumber)
            .MaximumLength(20).WithMessage("Label must not exceed 20 characters.")
            .When(x => x.LabelNumber is not null);
    }
}
