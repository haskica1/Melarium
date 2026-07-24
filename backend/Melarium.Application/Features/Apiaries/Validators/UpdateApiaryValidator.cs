using Melarium.Application.Features.Apiaries.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Apiaries.Validators;

public class UpdateApiaryValidator : AbstractValidator<UpdateApiaryDto>
{
    public UpdateApiaryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Apiary name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90.")
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180.")
            .When(x => x.Longitude.HasValue);
    }
}
