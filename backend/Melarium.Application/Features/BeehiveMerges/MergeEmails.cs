using Melarium.Application.Common.Email;
using Melarium.Domain.Entities;

namespace Melarium.Application.Features.BeehiveMerges;

/// <summary>A merge, as an e-mail (ADR-048): which hive is gone, and where its colony went.</summary>
public static class MergeEmails
{
    public static EmailContent Merged(Beehive source, Beehive target, Apiary apiary, User actor) => new("Sastavljena društva")
    {
        Subject = $"Sastavljena društva: {source.Name} → {target.Name}",
        Blocks =
        [
            new EmailText($"Košnica '{source.Name}' više nije u pčelinjaku — njeno društvo je sada u košnici '{target.Name}'."),
            new EmailFacts(
            [
                new EmailFact("Pčelinjak", apiary.Name),
                new EmailFact("Spojeno", $"{source.Name} → {target.Name}"),
                new EmailFact("Sastavio/la", $"{actor.FirstName} {actor.LastName}"),
            ]),
        ],
        Button = new EmailLink($"/beehives/{target.Id}", $"Otvori košnicu {target.Name}"),
    };
}
