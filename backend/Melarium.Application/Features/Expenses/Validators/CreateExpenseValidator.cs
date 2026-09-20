using Melarium.Application.Features.Expenses.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Expenses.Validators;

public class CreateExpenseValidator : AbstractValidator<CreateExpenseDto>
{
    public CreateExpenseValidator()
    {
        RuleFor(x => x.Source)
            .IsInEnum().WithMessage("Invalid expense source.");

        // Whether the apiary belongs to the caller's organization is a service-layer check —
        // it needs a DB lookup, which validators in this codebase don't do (SPEC-25 D1).
        RuleFor(x => x.ApiaryId)
            .GreaterThan(0).WithMessage("Neispravan pčelinjak.")
            .When(x => x.ApiaryId.HasValue);

        RuleFor(x => x.PurchaseDate)
            .NotEmpty().WithMessage("Purchase date is required.");

        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Total amount must be 0 or greater.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .MaximumLength(10).WithMessage("Currency must not exceed 10 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.")
            .When(x => x.Notes is not null);

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one expense item is required.");

        RuleForEach(x => x.Items).SetValidator(new ExpenseItemValidator());
    }
}
