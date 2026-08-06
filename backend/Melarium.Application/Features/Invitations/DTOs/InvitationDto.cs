using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Invitations.DTOs;

/// <summary>One row in the inviter's own history on /invite (SPEC-15).</summary>
/// <param name="StatusName">
/// Bosnian label the UI shows as-is. Deliberately three states, not two: a registered invitee who
/// has not confirmed their address yet reads "Registrovao se — čeka potvrdu", never a blank cell and
/// never "pridružio se", because the inviter's reward is waiting on exactly that step.
/// </param>
public record InvitationDto(
    int Id,
    InvitationSource Source,
    string SourceName,
    string Email,
    InvitationStatus Status,
    string StatusName,
    int? RewardDays,
    DateTime? AcceptedAt,
    DateTime CreatedAt);
