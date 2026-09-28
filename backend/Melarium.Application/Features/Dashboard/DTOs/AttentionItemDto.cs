namespace Melarium.Application.Features.Dashboard.DTOs;

/// <summary>One thing that needs doing, for one apiary (SPEC-29).</summary>
/// <param name="Kind">The <c>NotificationType</c> name of the rule that found it.</param>
/// <param name="Priority">The <c>NotificationPriority</c> name the rule has in this phase.</param>
/// <param name="Items">What it is about — hive names with detail, when there are several.</param>
public record AttentionItemDto(
    string Kind,
    string Priority,
    int ApiaryId,
    string ApiaryName,
    string Text,
    IReadOnlyList<string> Items,
    string LinkPath);
