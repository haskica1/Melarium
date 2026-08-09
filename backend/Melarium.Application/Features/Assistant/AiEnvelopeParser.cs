using System.Globalization;
using System.Text.Json;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Assistant;

/// <summary>
/// Turns the model's raw JSON into a typed <see cref="AiEnvelope"/> (SPEC-17 §3). Pure and total:
/// it never throws on bad model output. A response that cannot be read at all yields <c>null</c>;
/// an individual action that cannot be read is <b>dropped</b> so the rest of the turn still works.
/// </summary>
public static class AiEnvelopeParser
{
    private static readonly JsonDocumentOptions DocOptions = new() { AllowTrailingCommas = true };

    /// <summary>Parses one envelope, or returns null when the payload is not usable JSON.</summary>
    public static AiEnvelope? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        // Models occasionally wrap JSON in a ```json fence despite response_format — strip it rather
        // than losing an otherwise perfectly good turn.
        var payload = StripCodeFence(json.Trim());

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(payload, DocOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var root = doc.RootElement;

            var reply   = ReadString(root, "reply") ?? string.Empty;
            var needs   = ReadClarification(root);
            var actions = ReadActions(root);

            return new AiEnvelope(reply, needs, actions);
        }
    }

    // ── Sections ──────────────────────────────────────────────────────────────

    private static AiClarification? ReadClarification(JsonElement root)
    {
        if (!root.TryGetProperty("needs", out var needs) || needs.ValueKind != JsonValueKind.Object)
            return null;

        var question = ReadString(needs, "question");
        if (string.IsNullOrWhiteSpace(question)) return null;

        var what = ReadString(needs, "what") ?? "";
        return new AiClarification(what.ToLowerInvariant(), question);
    }

    private static IReadOnlyList<AiProposedAction> ReadActions(JsonElement root)
    {
        if (!root.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<AiProposedAction>();
        foreach (var element in actions.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) continue;

            var kind = ReadKind(element);
            if (kind is null) continue; // unknown action kind — drop it, keep the rest

            result.Add(new AiProposedAction(
                kind.Value,
                Trimmed(ReadString(element, "apiary")),
                ReadHives(element),
                ReadFields(element),
                Trimmed(ReadString(element, "targetTitle"))));
        }
        return result;
    }

    private static AiActionKind? ReadKind(JsonElement action) => ReadString(action, "kind")?.Trim().ToLowerInvariant() switch
    {
        "create_inspection" => AiActionKind.CreateInspection,
        "create_todo"       => AiActionKind.CreateTodo,
        "update_todo"       => AiActionKind.UpdateTodo,
        "complete_todo"     => AiActionKind.CompleteTodo,
        "update_inspection" => AiActionKind.UpdateInspection,
        "delete_todo"       => AiActionKind.DeleteTodo,
        "delete_inspection" => AiActionKind.DeleteInspection,
        _                   => null,
    };

    private static AiHiveSelector ReadHives(JsonElement action)
    {
        if (!action.TryGetProperty("hives", out var hives)) return AiHiveSelector.None;

        // "all" — the literal the prompt asks for when the beekeeper says "sve košnice".
        if (hives.ValueKind == JsonValueKind.String)
        {
            var single = hives.GetString()?.Trim();
            if (string.IsNullOrEmpty(single)) return AiHiveSelector.None;
            return IsAll(single) ? new AiHiveSelector(true, []) : new AiHiveSelector(false, [single]);
        }

        if (hives.ValueKind != JsonValueKind.Array) return AiHiveSelector.None;

        var numbers = new List<string>();
        foreach (var item in hives.EnumerateArray())
        {
            // The model is asked for strings but sometimes emits bare numbers.
            var value = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString()?.Trim(),
                JsonValueKind.Number => item.TryGetInt32(out var n) ? n.ToString(CultureInfo.InvariantCulture) : null,
                _                    => null,
            };
            if (string.IsNullOrEmpty(value)) continue;
            if (IsAll(value)) return new AiHiveSelector(true, []);
            if (!numbers.Contains(value)) numbers.Add(value);
        }
        return numbers.Count == 0 ? AiHiveSelector.None : new AiHiveSelector(false, numbers);
    }

    private static AiActionFields ReadFields(JsonElement action)
    {
        if (!action.TryGetProperty("fields", out var f) || f.ValueKind != JsonValueKind.Object)
            return new AiActionFields();

        return new AiActionFields(
            Date:        ReadDate(f, "date"),
            HoneyLevel:  ReadEnumInRange<HoneyLevel>(f, "honeyLevel", 1, 3),
            BroodStatus: Trimmed(ReadString(f, "broodStatus")),
            Notes:       Trimmed(ReadString(f, "notes")),
            Title:       Trimmed(ReadString(f, "title")),
            Priority:    ReadEnumInRange<TodoPriority>(f, "priority", 1, 3),
            DueDate:     ReadDate(f, "dueDate"));
    }

    // ── Primitives ────────────────────────────────────────────────────────────

    private static string? ReadString(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>Strict <c>yyyy-MM-dd</c>; anything else (prose, a full timestamp, nonsense) becomes null.</summary>
    private static DateOnly? ReadDate(JsonElement parent, string name)
    {
        var raw = ReadString(parent, name)?.Trim();
        if (string.IsNullOrEmpty(raw)) return null;

        return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    /// <summary>
    /// Reads an integer enum value, rejecting anything outside the declared range. Casting a stray 0
    /// or 7 straight to the enum would sail past <c>IsInEnum()</c>-free code paths and land a
    /// meaningless value in the database.
    /// </summary>
    private static T? ReadEnumInRange<T>(JsonElement parent, string name, int min, int max) where T : struct, Enum
    {
        if (!parent.TryGetProperty(name, out var v)) return null;

        int? value = v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetInt32(out var n) ? n : null,
            JsonValueKind.String => int.TryParse(v.GetString(), out var s) ? s : null,
            _                    => null,
        };

        if (value is null || value < min || value > max) return null;
        return (T)(object)value.Value;
    }

    private static string? Trimmed(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static bool IsAll(string value) =>
        value.Equals("all", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("sve", StringComparison.OrdinalIgnoreCase);

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;

        var firstNewline = text.IndexOf('\n');
        if (firstNewline < 0) return text;

        var body = text[(firstNewline + 1)..];
        var closing = body.LastIndexOf("```", StringComparison.Ordinal);
        return (closing >= 0 ? body[..closing] : body).Trim();
    }
}
