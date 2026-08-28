using Melarium.Application.Features.Announcements.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Announcements.Validators;

public class SaveAnnouncementValidator : AbstractValidator<SaveAnnouncementDto>
{
    public SaveAnnouncementValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Naslov je obavezan.")
            .MaximumLength(150).WithMessage("Naslov može imati najviše 150 znakova.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Nepoznat tip objave.");

        // A draft may have an empty body; publish enforces content in the service.
    }
}
