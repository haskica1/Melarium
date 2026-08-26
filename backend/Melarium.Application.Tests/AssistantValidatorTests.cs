using Melarium.Application.Features.Assistant.DTOs;
using Melarium.Application.Features.Assistant.Validators;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// The confirm request is untrusted input that <c>AiAssistantService.ConfirmAsync</c> immediately
/// keys by action id. Anything the validator lets through in a shape that dictionary cannot take
/// becomes a 500 rather than a readable rejection, so these rules are a guard, not a formality.
/// </summary>
public class ConfirmActionsValidatorTests
{
    private readonly ConfirmActionsValidator _validator = new();

    [Fact]
    public void A_repeated_action_id_is_rejected()
    {
        var result = _validator.Validate(new ConfirmActionsDto
        {
            Actions = [new ConfirmActionItemDto { Id = 1 }, new ConfirmActionItemDto { Id = 1 }],
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Ista radnja je poslana više puta.");
    }

    [Fact]
    public void Distinct_action_ids_pass()
    {
        var result = _validator.Validate(new ConfirmActionsDto
        {
            Actions = [new ConfirmActionItemDto { Id = 1 }, new ConfirmActionItemDto { Id = 2 }],
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void An_empty_list_stays_valid_because_it_means_reject_everything()
    {
        Assert.True(_validator.Validate(new ConfirmActionsDto()).IsValid);
    }

    [Fact]
    public void A_null_list_is_rejected_rather_than_dereferenced_downstream()
    {
        var result = _validator.Validate(new ConfirmActionsDto { Actions = null! });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Nedostaje lista radnji.");
    }
}
