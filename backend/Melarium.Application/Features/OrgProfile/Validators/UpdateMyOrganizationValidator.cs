using Melarium.Application.Features.OrgProfile.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.OrgProfile.Validators;

public class UpdateMyOrganizationValidator : AbstractValidator<UpdateMyOrganizationDto>
{
    public UpdateMyOrganizationValidator()
    {
        // Same bounds as the columns and as the SystemAdmin form, so an org cannot be saved through
        // one screen in a shape the other refuses.
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Naziv organizacije je obavezan.")
            .MaximumLength(200).WithMessage("Naziv ne smije biti duži od 200 znakova.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Opis ne smije biti duži od 1000 znakova.");
    }
}
