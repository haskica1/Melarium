using System.Text.Json;
using System.Text.Json.Serialization;

namespace Melarium.Application.Features.Assistant;

/// <summary>
/// What one proposed action would do, as stored in <c>AiAssistantAction.PayloadJson</c> and shown on
/// the card. Target ids may be null while <see cref="Issue"/> says why — an unresolved action still
/// gets a row so the user has something to complete, rather than losing what they said.
///
/// <see cref="ExistingEntityId"/> is set only for a Phase C update/complete/delete action. It is fixed
/// at propose time and never re-picked from the confirm request (SPEC-17 §5.3) — unlike a create
/// action's apiary/hive, there is no dropdown that lets the user swap in a different existing record.
/// <see cref="PreviousFields"/> is that record's current values, carried along so the card can show
/// "prije → poslije" without a second fetch, and so it still renders correctly after a page reload.
/// </summary>
public sealed record AiActionPayload(
    int? ApiaryId,
    string? ApiaryName,
    int? BeehiveId,
    string? BeehiveName,
    AiResolutionIssue Issue,
    IReadOnlyList<string> UnresolvedHiveNumbers,
    AiActionFields Fields,
    int? ExistingEntityId = null,
    string? ExistingEntityType = null,
    string? ExistingSummary = null,
    AiActionFields? PreviousFields = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Reads a stored payload; returns null rather than throwing on a row written by older code.</summary>
    public static AiActionPayload? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<AiActionPayload>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
