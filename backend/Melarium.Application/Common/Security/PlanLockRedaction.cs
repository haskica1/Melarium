using Melarium.Application.Features.Apiaries.DTOs;
using Melarium.Application.Features.Beehives.DTOs;
using Melarium.Domain.Common;

namespace Melarium.Application.Common.Security;

/// <summary>
/// What a locked row (SPEC-24) is allowed to reveal, defined once. A locked apiary or hive stays in
/// its list — that is the point, the beekeeper should see what an upgrade brings back — but it
/// carries nothing beyond its name: no notes, no coordinates, no counts, no QR identity.
///
/// This lives in one place on purpose. Marking rows as locked in each list endpoint separately is
/// how a field quietly keeps leaking from the one endpoint nobody updated.
/// </summary>
public static class PlanLockRedaction
{
    /// <summary>Flags and strips the apiaries this organization can no longer reach.</summary>
    public static IEnumerable<T> Redact<T>(this IEnumerable<T> apiaries, PlanLockResult locked)
        where T : ApiaryDto
    {
        if (locked.ApiaryIds.Count == 0) return apiaries;

        foreach (var dto in apiaries)
        {
            if (!locked.ApiaryIds.Contains(dto.Id)) continue;

            dto.IsLocked = true;
            dto.Description = null;
            dto.Latitude = null;
            dto.Longitude = null;
            dto.HomeLatitude = null;
            dto.HomeLongitude = null;
            dto.CreatedByName = null;

            // Zero rather than "20": the frontend hides the counter entirely for a locked card, and a
            // number that reached the UI anyway must not be a true one.
            dto.BeehiveCount = 0;
        }

        return apiaries;
    }

    /// <summary>Flags and strips the beehives this organization can no longer reach.</summary>
    public static IEnumerable<BeehiveDto> Redact(this IEnumerable<BeehiveDto> beehives, PlanLockResult locked)
    {
        if (locked.BeehiveIds.Count == 0) return beehives;

        foreach (var dto in beehives)
        {
            if (!locked.BeehiveIds.Contains(dto.Id)) continue;

            dto.IsLocked = true;
            dto.Notes = null;
            dto.LabelNumber = null;
            dto.TypeName = string.Empty;
            dto.MaterialName = string.Empty;
            dto.CreatedByName = null;
            dto.InspectionCount = 0;

            // The QR identity is a key, not a label: leaving it in would let the scan flow resolve a
            // hive the plan has locked away.
            dto.UniqueId = null;
        }

        return beehives;
    }
}
