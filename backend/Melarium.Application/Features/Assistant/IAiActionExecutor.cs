using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Assistant;

/// <summary>
/// Executes one confirmed assistant action through the existing application services (SPEC-17 §5.1).
/// Behind an interface so the service can be unit-tested without touching inspection or todo internals.
/// </summary>
public interface IAiActionExecutor
{
    /// <summary>Never throws for an expected failure — a denial or a validation error comes back in the outcome.</summary>
    Task<AiActionOutcome> ExecuteAsync(AiActionKind kind, AiActionPayload payload);
}
