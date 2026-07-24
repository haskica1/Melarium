using AutoMapper;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Inspections;
using Melarium.Application.Features.Weather;
using Melarium.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>Deleting an inspection also removes its photo blobs — best-effort (SPEC-05).</summary>
public class InspectionServiceDeleteTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();

    private InspectionService Service() => new(
        _uow,
        Substitute.For<IMapper>(),
        _access,
        Substitute.For<IWeatherService>(),
        _storage,
        Substitute.For<ILogger<InspectionService>>());

    [Fact]
    public async Task Delete_InspectionWithPhotos_DeletesBlobs()
    {
        _uow.Inspections.GetByIdAsync(10).Returns(new Inspection { Id = 10, BeehiveId = 3 });
        _uow.InspectionPhotos.GetByInspectionIdAsync(10).Returns(new[]
        {
            new InspectionPhoto { Id = 1, InspectionId = 10, StoragePath = "a" },
            new InspectionPhoto { Id = 2, InspectionId = 10, StoragePath = "b" },
        });

        await Service().DeleteAsync(10);

        await _uow.Inspections.Received(1).DeleteAsync(Arg.Any<Inspection>());
        await _uow.Received(1).SaveChangesAsync();
        await _storage.Received(1).DeleteAsync("a", Arg.Any<CancellationToken>());
        await _storage.Received(1).DeleteAsync("b", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_BlobFailure_NeverBlocksTheDelete()
    {
        _uow.Inspections.GetByIdAsync(10).Returns(new Inspection { Id = 10, BeehiveId = 3 });
        _uow.InspectionPhotos.GetByInspectionIdAsync(10).Returns(new[]
        {
            new InspectionPhoto { Id = 1, InspectionId = 10, StoragePath = "a" },
            new InspectionPhoto { Id = 2, InspectionId = 10, StoragePath = "b" },
        });
        _storage.DeleteAsync("a", Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("down"));

        await Service().DeleteAsync(10); // must not throw

        await _uow.Received(1).SaveChangesAsync();
        // The failure on "a" must not stop "b" from being attempted.
        await _storage.Received(1).DeleteAsync("b", Arg.Any<CancellationToken>());
    }
}
