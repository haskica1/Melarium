namespace Melarium.Application.Features.Ai;

/// <summary>Vision-model client that reads the number/label painted on a hive from a photo.</summary>
public interface IHiveNumberOcrClient
{
    /// <summary>
    /// Sends the image to the vision model and returns the recognised number/label.
    /// Throws <see cref="Common.Exceptions.BusinessRuleException"/> for images too large for the
    /// provider's request limit. Unreadable images yield a result with a null <c>Number</c>, not an error.
    /// </summary>
    Task<HiveNumberOcrResult> RecognizeNumberAsync(byte[] imageBytes, string contentType, CancellationToken cancellationToken = default);
}

/// <summary>What the model read: <see cref="Number"/> is null when no clear number/label was visible.</summary>
public record HiveNumberOcrResult(string? Number, double? Confidence);
