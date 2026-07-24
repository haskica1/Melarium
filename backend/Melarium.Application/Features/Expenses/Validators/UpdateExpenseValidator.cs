using Melarium.Application.Features.Expenses.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Expenses.Validators;

public class UpdateExpenseValidator : AbstractValidator<UpdateExpenseDto>
{
    public UpdateExpenseValidator()
    {
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
