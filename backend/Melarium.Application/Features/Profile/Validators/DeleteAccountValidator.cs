using Melarium.Application.Features.Profile.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Profile.Validators;

public class DeleteAccountValidator : AbstractValidator<DeleteAccountDto>
{
    public DeleteAccountValidator()
    {
        // Only presence is checked here. Whether it is the *right* password is decided in the
        // service against the stored hash, and deliberately not treated as a validation error:
        // a wrong password must cost the same and say the same thing as any other refusal.
        //
        // No `.Password()` policy either — this is an existing password being re-typed, and an
        // account created before the current minimum length must still be deletable.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Unesite lozinku da potvrdite brisanje računa.");
    }
}
