using Melarium.Application.Features.OrgManagement.DTOs;
using FluentValidation;

namespace Melarium.Application.Features.OrgManagement.Validators;

public class TransferOwnershipValidator : AbstractValidator<TransferOwnershipDto>
{
    public TransferOwnershipValidator()
    {
        // Shape only. Whether the id belongs to the caller's organization — and whether it is the
        // caller themselves — is decided in the service, where the organization is known.
        RuleFor(x => x.MemberId)
            .GreaterThan(0).WithMessage("Odaberite člana na kojeg prenosite vlasništvo.");
    }
}
