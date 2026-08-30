using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Features.OrgProfile;
using Melarium.Application.Features.OrgProfile.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// "Moja organizacija" (SPEC-22). What is worth locking here is not the property copying but the
/// three things that would be silent if they broke: the organization always comes from the caller's
/// JWT (never from an id the client could supply), a replaced logo does not leave its old blob
/// behind, and an upload is judged by its header bytes rather than by what the client claimed.
/// </summary>
public class OrgProfileServiceTests
{
    private const int OrgId = 5;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();

    private static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private OrgProfileService Service(ICurrentUser user) =>
        new(_uow, user, _storage, NullLogger<OrgProfileService>.Instance);

    private static TestCurrentUser OrgAdmin => new()
    {
        UserId = 1, Role = UserRole.OrganizationAdmin, OrganizationId = OrgId,
    };

    private static TestCurrentUser SystemAdmin => new()
    {
        UserId = 99, Role = UserRole.SystemAdmin, OrganizationId = null,
    };

    private Organization GivenOrganization(string? logoPath = null)
    {
        var org = new Organization
        {
            Id = OrgId,
            Name = "Zlatna košnica",
            Description = "Stari opis",
            LogoStoragePath = logoPath,
            LogoContentType = logoPath is null ? null : "image/png",
        };
        _uow.Organizations.GetWithDetailsAsync(OrgId).Returns(org);
        _uow.Organizations.GetBeehiveCountsAsync(OrgId).Returns(new Dictionary<int, int> { [OrgId] = 12 });
        return org;
    }

    // ── Tenant scoping ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WritesToTheCallersOwnOrganization_ResolvedFromTheToken()
    {
        var org = GivenOrganization();

        var result = await Service(OrgAdmin).UpdateMyOrganizationAsync(
            new UpdateMyOrganizationDto("  Medna dolina  ", "  Novi opis  "));

        // The id was never an argument — only the token could have selected this row.
        await _uow.Organizations.Received(1).GetWithDetailsAsync(OrgId);
        Assert.Equal("Medna dolina", org.Name);
        Assert.Equal("Novi opis", org.Description);
        Assert.Equal(OrgId, result.Id);
        Assert.Equal(12, result.BeehiveCount);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Update_BlankDescription_IsStoredAsNullRatherThanAnEmptyString()
    {
        var org = GivenOrganization();

        await Service(OrgAdmin).UpdateMyOrganizationAsync(new UpdateMyOrganizationDto("Medna dolina", "   "));

        Assert.Null(org.Description);
    }

    [Fact]
    public async Task AnOrglessSystemAdmin_IsRefused_RatherThanFallingThroughToSomeoneElsesOrg()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => Service(SystemAdmin).GetMyOrganizationAsync());

        await _uow.Organizations.DidNotReceive().GetWithDetailsAsync(Arg.Any<int>());
    }

    // ── Logo ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetLogo_ReplacingAnExistingOne_DeletesTheOldBlobAfterTheNewKeyIsCommitted()
    {
        var org = GivenOrganization(logoPath: "orgs/old-logo.png");
        _storage.SaveAsync(Arg.Any<Stream>(), "image/png").Returns("orgs/new-logo.png");

        await Service(OrgAdmin).SetLogoAsync(new MemoryStream(Png), Png.Length);

        Assert.Equal("orgs/new-logo.png", org.LogoStoragePath);
        Assert.Equal("image/png", org.LogoContentType);
        await _storage.Received(1).DeleteAsync("orgs/old-logo.png");
        // Never the one just stored — that is the bug this guards against.
        await _storage.DidNotReceive().DeleteAsync("orgs/new-logo.png");
    }

    [Fact]
    public async Task SetLogo_JudgesTheFormatByHeaderBytes_NotByWhatTheClientSent()
    {
        GivenOrganization();
        var notAnImage = new MemoryStream("<?php echo 1; ?>"u8.ToArray());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service(OrgAdmin).SetLogoAsync(notAnImage, notAnImage.Length));

        await _storage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SetLogo_OverTheSizeCap_IsRefusedBeforeAnythingIsStored()
    {
        GivenOrganization();

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Service(OrgAdmin).SetLogoAsync(new MemoryStream(Png), OrgProfileService.MaxLogoBytes + 1));

        await _storage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RemoveLogo_ClearsBothColumnsAndDeletesTheBlob()
    {
        var org = GivenOrganization(logoPath: "orgs/logo.png");

        var result = await Service(OrgAdmin).RemoveLogoAsync();

        Assert.Null(org.LogoStoragePath);
        Assert.Null(org.LogoContentType);
        Assert.False(result.HasLogo);
        await _storage.Received(1).DeleteAsync("orgs/logo.png");
    }

    [Fact]
    public async Task OpenLogo_WhenTheOrganizationHasNone_Is404RatherThanAnEmptyStream()
    {
        GivenOrganization();

        await Assert.ThrowsAsync<NotFoundException>(() => Service(OrgAdmin).OpenLogoAsync());
    }

    [Fact]
    public async Task OpenLogo_WhenTheRowPointsAtAMissingBlob_Is404RatherThan500()
    {
        GivenOrganization(logoPath: "orgs/gone.png");
        _storage.OpenReadAsync("orgs/gone.png").Returns<Stream>(_ => throw new FileNotFoundException());

        await Assert.ThrowsAsync<NotFoundException>(() => Service(OrgAdmin).OpenLogoAsync());
    }
}
