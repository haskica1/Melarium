using AutoMapper;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Ai;
using Melarium.Application.Features.Inspections;
using Melarium.Application.Features.Inspections.DTOs;
using Melarium.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Inspection photos (SPEC-05): upload rules (count / size / real content type),
/// access via the parent inspection, and blob cleanup semantics.
/// </summary>
public class InspectionPhotoServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
    private readonly IPhotoAnalysisAiClient _vision = Substitute.For<IPhotoAnalysisAiClient>();
    private readonly IPlanGuard _plan = Substitute.For<IPlanGuard>();

    private InspectionPhotoService Service() =>
        new(_uow, _mapper, _access, _storage, _vision,
            new TestCurrentUser { UserId = 1, Role = Domain.Enums.UserRole.OrganizationAdmin, OrganizationId = 1 },
            _plan,
            Substitute.For<ILogger<InspectionPhotoService>>());

    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    private static readonly byte[] PngHeader  = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] WebpHeader = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

    private void SetupInspection(int inspectionId = 10, int beehiveId = 3, int existingPhotos = 0)
    {
        _uow.Inspections.GetByIdAsync(inspectionId)
            .Returns(new Inspection { Id = inspectionId, BeehiveId = beehiveId });
        _uow.InspectionPhotos.CountByInspectionAsync(inspectionId).Returns(existingPhotos);
        _mapper.Map<InspectionPhotoDto>(Arg.Any<InspectionPhoto>())
            .Returns(ci => new InspectionPhotoDto { Id = ci.Arg<InspectionPhoto>().Id });
    }

    // ── Upload rules ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_SixthPhoto_ThrowsBusinessRule_NothingStored()
    {
        SetupInspection(existingPhotos: 5);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service().AddAsync(10, new MemoryStream(JpegHeader), 100, null));

        Assert.Contains("najviše 5 fotografija", ex.Message);
        await _storage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _uow.InspectionPhotos.DidNotReceive().AddAsync(Arg.Any<InspectionPhoto>());
    }

    [Fact]
    public async Task Add_Over8Mb_ThrowsBusinessRule_NothingStored()
    {
        SetupInspection();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service().AddAsync(10, new MemoryStream(JpegHeader), 9_000_000, null));

        Assert.Contains("8 MB", ex.Message);
        await _storage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_NonImageBytes_ThrowsBusinessRule_EvenWithImageExtensionClaim()
    {
        SetupInspection();
        var fakeJpeg = "GIF89a not really a jpeg"u8.ToArray();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service().AddAsync(10, new MemoryStream(fakeJpeg), 100, null));

        Assert.Contains("JPEG, PNG i WebP", ex.Message);
        await _storage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D }, "image/png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    public async Task Add_RealImageHeader_DetectsContentTypeFromBytes(byte[] header, string expectedContentType)
    {
        SetupInspection();
        _storage.SaveAsync(Arg.Any<Stream>(), expectedContentType, Arg.Any<CancellationToken>())
            .Returns("2026/07/key.bin");

        await Service().AddAsync(10, new MemoryStream(header), header.Length, "  moj opis  ");

        await _storage.Received(1).SaveAsync(Arg.Any<Stream>(), expectedContentType, Arg.Any<CancellationToken>());
        await _uow.InspectionPhotos.Received(1).AddAsync(Arg.Is<InspectionPhoto>(p =>
            p.InspectionId == 10 &&
            p.StoragePath == "2026/07/key.bin" &&
            p.ContentType == expectedContentType &&
            p.Caption == "moj opis"));
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Add_NonSeekableStream_IsBufferedAndStored()
    {
        SetupInspection();
        _storage.SaveAsync(Arg.Any<Stream>(), "image/png", Arg.Any<CancellationToken>()).Returns("k");

        await Service().AddAsync(10, new NonSeekableStream(PngHeader), PngHeader.Length, null);

        await _storage.Received(1).SaveAsync(Arg.Any<Stream>(), "image/png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_DbInsertFails_BlobIsCleanedUp()
    {
        SetupInspection();
        _storage.SaveAsync(Arg.Any<Stream>(), "image/webp", Arg.Any<CancellationToken>()).Returns("orphan-key");
        _uow.SaveChangesAsync().ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service().AddAsync(10, new MemoryStream(WebpHeader), 100, null));

        await _storage.Received(1).DeleteAsync("orphan-key", Arg.Any<CancellationToken>());
    }

    // ── Authorization ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_CallerWithoutBeehiveAccess_Forbidden()
    {
        SetupInspection(beehiveId: 3);
        _access.EnsureCanAccessBeehiveAsync(3).ThrowsAsync(new ForbiddenAccessException("ne"));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            Service().AddAsync(10, new MemoryStream(JpegHeader), 100, null));
    }

    [Fact]
    public async Task OpenFile_ChecksAccessOfParentInspection()
    {
        _uow.InspectionPhotos.GetByIdAsync(7)
            .Returns(new InspectionPhoto { Id = 7, InspectionId = 10, StoragePath = "p", ContentType = "image/jpeg" });
        _uow.Inspections.GetByIdAsync(10).Returns(new Inspection { Id = 10, BeehiveId = 3 });
        _access.EnsureCanAccessBeehiveAsync(3).ThrowsAsync(new ForbiddenAccessException("ne"));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service().OpenFileAsync(7));
        await _storage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Reads / deletes ────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenFile_MissingBlob_MapsToNotFound()
    {
        _uow.InspectionPhotos.GetByIdAsync(7)
            .Returns(new InspectionPhoto { Id = 7, InspectionId = 10, StoragePath = "gone", ContentType = "image/jpeg" });
        _uow.Inspections.GetByIdAsync(10).Returns(new Inspection { Id = 10, BeehiveId = 3 });
        _storage.OpenReadAsync("gone", Arg.Any<CancellationToken>())
            .ThrowsAsync(new FileNotFoundException());

        await Assert.ThrowsAsync<NotFoundException>(() => Service().OpenFileAsync(7));
    }

    [Fact]
    public async Task Delete_RemovesRowAndBlob()
    {
        var photo = new InspectionPhoto { Id = 7, InspectionId = 10, StoragePath = "blob-key" };
        _uow.InspectionPhotos.GetByIdAsync(7).Returns(photo);
        _uow.Inspections.GetByIdAsync(10).Returns(new Inspection { Id = 10, BeehiveId = 3 });

        await Service().DeleteAsync(7);

        await _uow.InspectionPhotos.Received(1).DeleteAsync(photo);
        await _uow.Received(1).SaveChangesAsync();
        await _storage.Received(1).DeleteAsync("blob-key", Arg.Any<CancellationToken>());
    }

    // ── AI analysis (Phase 2) ──────────────────────────────────────────────────

    [Fact]
    public async Task Analyze_PersistsResultJson_AndReturnsUpdatedDto()
    {
        var photo = new InspectionPhoto { Id = 7, InspectionId = 10, StoragePath = "p", ContentType = "image/jpeg" };
        _uow.InspectionPhotos.GetByIdAsync(7).Returns(photo);
        _uow.Inspections.GetByIdAsync(10).Returns(new Inspection { Id = 10, BeehiveId = 3 });
        _storage.OpenReadAsync("p", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(JpegHeader));
        _vision.AnalyzeFrameAsync(Arg.Any<byte[]>(), "image/jpeg", Arg.Any<CancellationToken>())
            .Returns(new PhotoAnalysisResult { IsFramePhoto = true, BroodPattern = 4, Anomalies = ["moguće rojenje"] });
        _mapper.Map<InspectionPhotoDto>(photo)
            .Returns(ci => new InspectionPhotoDto { Id = 7, AnalysisJson = photo.AnalysisJson });

        var dto = await Service().AnalyzeAsync(7);

        Assert.NotNull(photo.AnalysisJson);
        Assert.Contains("\"isFramePhoto\":true", photo.AnalysisJson);
        Assert.Contains("\"broodPattern\":4", photo.AnalysisJson);
        Assert.Contains("moguće rojenje", photo.AnalysisJson);
        Assert.Equal(photo.AnalysisJson, dto.AnalysisJson);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Analyze_VisionFailure_LeavesPhotoUntouched()
    {
        var photo = new InspectionPhoto { Id = 7, InspectionId = 10, StoragePath = "p", ContentType = "image/jpeg" };
        _uow.InspectionPhotos.GetByIdAsync(7).Returns(photo);
        _uow.Inspections.GetByIdAsync(10).Returns(new Inspection { Id = 10, BeehiveId = 3 });
        _storage.OpenReadAsync("p", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(JpegHeader));
        _vision.AnalyzeFrameAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessRuleException("AI analiza nije uspjela — model je vratio neispravan odgovor. Pokušajte ponovo."));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service().AnalyzeAsync(7));

        Assert.Null(photo.AnalysisJson);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Delete_BlobFailure_DoesNotUndoDbDelete()
    {
        var photo = new InspectionPhoto { Id = 7, InspectionId = 10, StoragePath = "blob-key" };
        _uow.InspectionPhotos.GetByIdAsync(7).Returns(photo);
        _uow.Inspections.GetByIdAsync(10).Returns(new Inspection { Id = 10, BeehiveId = 3 });
        _storage.DeleteAsync("blob-key", Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("storage down"));

        await Service().DeleteAsync(7); // must not throw

        await _uow.InspectionPhotos.Received(1).DeleteAsync(photo);
        await _uow.Received(1).SaveChangesAsync();
    }

    /// <summary>Simulates request streams that cannot seek (forces the buffering path).</summary>
    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;
        public NonSeekableStream(byte[] data) => _inner = new MemoryStream(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
