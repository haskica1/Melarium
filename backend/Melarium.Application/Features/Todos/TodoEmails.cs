using Melarium.Application.Common.Email;
using Melarium.Application.Common.Localization;
using Melarium.Domain.Entities;

namespace Melarium.Application.Features.Todos;

/// <summary>The task e-mail (ADR-048); the bell keeps its one sentence.</summary>
public static class TodoEmails
{
    /// <summary>
    /// The task as a card — title, where, due date, priority, notes — under one sentence saying who
    /// made it, and a button to the page that lists it: the hive's, else the apiary's.
    /// </summary>
    public static EmailContent Created(string title, string lead, Todo todo, Beehive? beehive, Apiary? apiary)
    {
        var details = new List<string>();
        if (apiary != null) details.Add($"Pčelinjak {apiary.Name}");
        if (beehive != null) details.Add($"košnica {beehive.Name}");
        if (todo.DueDate is DateTime due) details.Add($"rok {due:dd.MM.yyyy.}");
        details.Add($"prioritet {BsLabels.Label(todo.Priority).ToLowerInvariant()}");

        var notes = todo.Notes?.Trim();
        if (notes is { Length: > 300 }) notes = notes[..300].TrimEnd() + "…";

        return new EmailContent(title)
        {
            Subject = apiary != null ? $"Novi zadatak u pčelinjaku {apiary.Name}" : "Novi zadatak",
            Blocks =
            [
                new EmailText(lead),
                new EmailCard(todo.Title) { Icon = "📋", Subtitle = string.Join(" · ", details), Text = notes },
            ],
            Button = new EmailLink(
                todo.BeehiveId is int hive ? $"/beehives/{hive}" : apiary != null ? $"/apiaries/{apiary.Id}" : "/",
                "Pogledaj zadatak"),
        };
    }
}
