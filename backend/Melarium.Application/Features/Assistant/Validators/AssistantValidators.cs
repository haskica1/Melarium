using FluentValidation;
using Melarium.Application.Features.Assistant.DTOs;

namespace Melarium.Application.Features.Assistant.Validators;

public class StartAssistantSessionValidator : AbstractValidator<StartAssistantSessionDto>
{
    public StartAssistantSessionValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Poruka je obavezna.")
            .MaximumLength(4000).WithMessage("Poruka ne smije prelaziti 4000 znakova.");

        RuleFor(x => x.ApiaryId)
            .GreaterThan(0).WithMessage("Neispravan pčelinjak.")
            .When(x => x.ApiaryId.HasValue);

        RuleFor(x => x.BeehiveId)
            .GreaterThan(0).WithMessage("Neispravna košnica.")
            .When(x => x.BeehiveId.HasValue);
    }
}

public class AssistantTurnRequestValidator : AbstractValidator<AssistantTurnRequestDto>
{
    public AssistantTurnRequestValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Poruka je obavezna.")
            .MaximumLength(4000).WithMessage("Poruka ne smije prelaziti 4000 znakova.");

        RuleFor(x => x.ApiaryId)
            .GreaterThan(0).WithMessage("Neispravan pčelinjak.")
            .When(x => x.ApiaryId.HasValue);

        RuleFor(x => x.BeehiveId)
            .GreaterThan(0).WithMessage("Neispravna košnica.")
            .When(x => x.BeehiveId.HasValue);
    }
}

public class ConfirmActionsValidator : AbstractValidator<ConfirmActionsDto>
{
    public ConfirmActionsValidator()
    {
        // ConfirmAsync keys the request by action id (`dto.Actions.ToDictionary(a => a.Id)`), so a
        // repeated id throws an ArgumentException the middleware can only render as a 500. A
        // duplicate is a malformed request, which belongs here as a 400 with a readable reason.
        RuleFor(x => x.Actions)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Nedostaje lista radnji.")
            .Must(actions => actions.Select(a => a.Id).Distinct().Count() == actions.Count)
                .WithMessage("Ista radnja je poslana više puta.");

        // An empty list is legitimate — it means "reject everything" — so only the shape is checked here.
        RuleForEach(x => x.Actions).ChildRules(action =>
        {
            action.RuleFor(a => a.Id)
                .GreaterThan(0).WithMessage("Neispravna radnja.");

            action.RuleFor(a => a.ApiaryId)
                .GreaterThan(0).WithMessage("Neispravan pčelinjak.")
                .When(a => a.ApiaryId.HasValue);

            action.RuleFor(a => a.BeehiveId)
                .GreaterThan(0).WithMessage("Neispravna košnica.")
                .When(a => a.BeehiveId.HasValue);
        });
    }
}
