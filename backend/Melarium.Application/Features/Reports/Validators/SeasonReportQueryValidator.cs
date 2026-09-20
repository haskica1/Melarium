using FluentValidation;
using Melarium.Application.Features.Reports.DTOs;

namespace Melarium.Application.Features.Reports.Validators;

public class SeasonReportQueryValidator : AbstractValidator<SeasonReportQueryDto>
{
    /// <summary>
    /// Five years covers "every season I have recorded" without letting one request sweep an entire
    /// organization's history into memory.
    /// </summary>
    private const int MaxRangeDays = 366 * 5;

    public SeasonReportQueryValidator()
    {
        RuleFor(x => x.From)
            .NotEqual(default(DateOnly)).WithMessage("Početni datum je obavezan.");

        RuleFor(x => x.To)
            .NotEqual(default(DateOnly)).WithMessage("Krajnji datum je obavezan.");

        RuleFor(x => x)
            .Must(q => q.From <= q.To)
            .WithMessage("Početni datum ne može biti poslije krajnjeg.")
            .WithName("to");

        RuleFor(x => x)
            .Must(q => q.To.DayNumber - q.From.DayNumber <= MaxRangeDays)
            .WithMessage("Period ne može biti duži od 5 godina.")
            .WithName("to")
            .When(q => q.From <= q.To);

        RuleFor(x => x.ApiaryId)
            .GreaterThan(0).WithMessage("Neispravan pčelinjak.")
            .When(x => x.ApiaryId.HasValue);
    }
}
