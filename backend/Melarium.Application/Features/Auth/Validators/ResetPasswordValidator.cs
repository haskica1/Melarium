using Melarium.Application.Common.Validation;
using Melarium.Application.Features.Auth.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Auth.Validators;

public class ResetPasswordValidator : AbstractValidator<ResetPasswordDto>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required.")
            .MaximumLength(128).WithMessage("Invalid reset token.");

        RuleFor(x => x.NewPassword).Password();
    }
}
