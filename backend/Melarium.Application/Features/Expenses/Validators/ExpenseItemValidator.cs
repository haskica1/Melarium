using Melarium.Application.Features.Expenses.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Expenses.Validators;

public class ExpenseItemValidator : AbstractValidator<CreateExpenseItemDto>
{
    public ExpenseItemValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0.");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Unit price must be 0 or greater.");

        RuleFor(x => x.TotalPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Total price must be 0 or greater.");

        RuleFor(x => x.Unit)
            .MaximumLength(50).WithMessage("Unit label must not exceed 50 characters.")
            .When(x => x.Unit is not null);

        // Whether the id actually belongs to the caller's organization is a service-layer check —
        // it needs a DB lookup, which validators in this codebase don't do.
        RuleFor(x => x.DietId)
            .GreaterThan(0).WithMessage("Neispravan program prehrane.")
            .When(x => x.DietId.HasValue);
    }
}
