using FluentValidation;
using Melarium.Application.Features.Notifications.DTOs;

namespace Melarium.Application.Features.Notifications.Validators;

public class UpdateNotificationSettingsValidator : AbstractValidator<UpdateNotificationSettingsDto>
{
    public UpdateNotificationSettingsValidator()
    {
        // Enums travel as numbers, so an out-of-range value binds without complaint unless refused here.
        RuleFor(x => x.EmailMode)
            .IsInEnum().WithMessage("Nepoznat način slanja e-maila.");
    }
}
