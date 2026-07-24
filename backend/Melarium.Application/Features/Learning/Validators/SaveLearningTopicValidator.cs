using Melarium.Application.Features.Learning.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.Learning.Validators;

public class SaveLearningTopicValidator : AbstractValidator<SaveLearningTopicDto>
{
    public SaveLearningTopicValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Naslov je obavezan.")
            .MaximumLength(150).WithMessage("Naslov može imati najviše 150 znakova.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Nepoznata kategorija.");

        RuleFor(x => x.Summary)
            .NotEmpty().WithMessage("Sažetak je obavezan.")
            .MaximumLength(300).WithMessage("Sažetak može imati najviše 300 znakova.");

        // A draft may have an empty body; publish enforces content in the service.
        RuleForEach(x => x.Months)
            .InclusiveBetween(1, 12).WithMessage("Mjeseci moraju biti između 1 i 12.")
            .When(x => x.Months is not null);

        RuleFor(x => x.VideoUrl)
            .MaximumLength(500).WithMessage("Video link može imati najviše 500 znakova.")
            .Must(BeAValidUrl).WithMessage("Video link mora biti validan URL (http/https).")
            .When(x => !string.IsNullOrWhiteSpace(x.VideoUrl));

        RuleFor(x => x.FileUrl)
            .MaximumLength(500).WithMessage("Link ka fajlu može imati najviše 500 znakova.")
            .Must(BeAValidUrl).WithMessage("Link ka fajlu mora biti validan URL (http/https).")
            .When(x => !string.IsNullOrWhiteSpace(x.FileUrl));

        RuleFor(x => x.FileName)
            .MaximumLength(150).WithMessage("Naziv fajla može imati najviše 150 znakova.");
    }

    private static bool BeAValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
