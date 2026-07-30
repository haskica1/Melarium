using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Localization;
using Melarium.Application.Common.Models;
using Melarium.Application.Features.Feedbacks.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Melarium.Application.Features.Feedbacks;

/// <summary>
/// User feedback (SPEC-13). Not tenant-scoped, so it does not use <c>IAccessGuard</c>: every read is
/// either scoped to the caller's own rows or reached through a SystemAdmin-only controller.
/// </summary>
public class FeedbackService : IFeedbackService
{
    /// <summary>Smaller than the 8 MB inspection-photo cap — this is a UI screenshot, not a frame photo.</summary>
    public const long MaxScreenshotBytes = 5 * 1024 * 1024;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notifications;
    private readonly IEmailQueue _emailQueue;
    private readonly IFileStorage _storage;
    private readonly IConfiguration _config;
    private readonly ILogger<FeedbackService> _logger;

    public FeedbackService(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        INotificationService notifications,
        IEmailQueue emailQueue,
        IFileStorage storage,
        IConfiguration config,
        ILogger<FeedbackService> logger)
    {
        _uow           = uow;
        _currentUser   = currentUser;
        _notifications = notifications;
        _emailQueue    = emailQueue;
        _storage       = storage;
        _config        = config;
        _logger        = logger;
    }

    // ── Submitter side ─────────────────────────────────────────────────────────

    public async Task<FeedbackDto> CreateAsync(CreateFeedbackDto dto)
    {
        var userId = RequireUserId();

        var feedback = new Feedback
        {
            UserId         = userId,
            OrganizationId = _currentUser.OrganizationId,
            Type           = dto.Type,
            Severity       = dto.Severity,
            Subject        = dto.Subject.Trim(),
            Message        = dto.Message.Trim(),
            PageContext    = Trimmed(dto.PageContext),
            UserAgent      = Trimmed(dto.UserAgent),
            Status         = FeedbackStatus.New,
        };

        await _uow.Feedbacks.AddAsync(feedback);
        await _uow.SaveChangesAsync();

        await NotifyAdminsAsync(feedback);

        return ToDto(feedback);
    }

    public async Task<IEnumerable<FeedbackDto>> GetMineAsync()
    {
        var userId = RequireUserId();
        var rows = await _uow.Feedbacks.GetByUserAsync(userId);
        return rows.Select(ToDto);
    }

    public async Task<FeedbackDto> GetMineByIdAsync(int id)
    {
        var feedback = await GetOwnOrThrowAsync(id);
        return ToDto(feedback);
    }

    public async Task<FeedbackDto> AttachScreenshotAsync(int id, Stream content, long sizeBytes)
    {
        var feedback = await GetOwnOrThrowAsync(id);

        if (feedback.ScreenshotStoragePath is not null)
            throw new BusinessRuleException("Ova prijava već ima priloženu sliku.");

        if (sizeBytes > MaxScreenshotBytes)
            throw new BusinessRuleException(
                $"Slika ne smije biti veća od {MaxScreenshotBytes / (1024 * 1024)} MB.");

        // The real type comes from the file's header bytes; the client-supplied type and the
        // extension are untrusted.
        var (seekable, contentType) = await SniffContentTypeAsync(content);
        if (contentType is null)
            throw new BusinessRuleException("Dozvoljeni formati slike su JPEG, PNG i WebP.");

        var storagePath = await _storage.SaveAsync(seekable, contentType);

        feedback.ScreenshotStoragePath = storagePath;
        feedback.ScreenshotContentType = contentType;

        try
        {
            await _uow.Feedbacks.UpdateAsync(feedback);
            await _uow.SaveChangesAsync();
        }
        catch
        {
            // The blob is already written — don't leave it orphaned if the DB update fails.
            await TryDeleteBlobAsync(storagePath);
            throw;
        }

        return ToDto(feedback);
    }

    public async Task<(Stream Content, string ContentType)> OpenScreenshotAsync(int id)
    {
        var feedback = await _uow.Feedbacks.GetByIdAsync(id)
            ?? throw new NotFoundException("Feedback", id);

        // The submitter and SystemAdmin may read it; anyone else gets a 404 rather than a 403, so
        // the endpoint cannot be used to probe which ids exist.
        var isOwner = feedback.UserId is int ownerId && ownerId == _currentUser.UserId;
        if (!isOwner && _currentUser.Role != UserRole.SystemAdmin)
            throw new NotFoundException("Feedback", id);

        if (feedback.ScreenshotStoragePath is null || feedback.ScreenshotContentType is null)
            throw new NotFoundException("FeedbackScreenshot", id);

        try
        {
            var stream = await _storage.OpenReadAsync(feedback.ScreenshotStoragePath);
            return (stream, feedback.ScreenshotContentType);
        }
        catch (FileNotFoundException)
        {
            // DB row exists but the blob is gone (e.g. wiped dev disk) — a clean 404 beats a 500.
            throw new NotFoundException("FeedbackScreenshot", id);
        }
    }

    // ── SystemAdmin side ───────────────────────────────────────────────────────

    public async Task<IEnumerable<AdminFeedbackDto>> GetAllForAdminAsync(
        FeedbackType? type, FeedbackStatus? status)
    {
        var rows = await _uow.Feedbacks.GetAllForAdminAsync(type, status);
        return rows.Select(ToAdminDto);
    }

    public async Task<AdminFeedbackDto> GetByIdForAdminAsync(int id)
    {
        var feedback = await _uow.Feedbacks.GetByIdWithUserAsync(id)
            ?? throw new NotFoundException("Feedback", id);
        return ToAdminDto(feedback);
    }

    public async Task<AdminFeedbackDto> UpdateStatusAsync(int id, UpdateFeedbackStatusDto dto)
    {
        var feedback = await _uow.Feedbacks.GetByIdAsync(id)
            ?? throw new NotFoundException("Feedback", id);

        var response = Trimmed(dto.AdminResponse);
        var responseChanged = response is not null && response != feedback.AdminResponse;
        var statusChanged   = dto.Status != feedback.Status;

        feedback.Status = dto.Status;

        if (responseChanged)
        {
            feedback.AdminResponse = response;
            feedback.RespondedAt   = DateTime.UtcNow;
            feedback.RespondedById = _currentUser.UserId;
        }

        await _uow.Feedbacks.UpdateAsync(feedback);
        await _uow.SaveChangesAsync();

        // Only bother the submitter when something they'd care about actually changed.
        if ((statusChanged || responseChanged) && feedback.UserId is int submitterId)
            await NotifySubmitterAsync(feedback, submitterId);

        return await GetByIdForAdminAsync(id);
    }

    public async Task<FeedbackSummaryDto> GetSummaryAsync() =>
        new(await _uow.Feedbacks.CountNewAsync());

    public async Task DeleteAsync(int id)
    {
        var feedback = await _uow.Feedbacks.GetByIdAsync(id)
            ?? throw new NotFoundException("Feedback", id);

        var storagePath = feedback.ScreenshotStoragePath;

        await _uow.Feedbacks.DeleteAsync(feedback);
        await _uow.SaveChangesAsync();

        if (storagePath is not null)
            await TryDeleteBlobAsync(storagePath);
    }

    // ── Notification ───────────────────────────────────────────────────────────

    /// <summary>
    /// Two channels on purpose. SystemAdmins get the in-app bell via the email-free broadcast, and a
    /// single email goes to the configured operator address — so adding a second SystemAdmin does not
    /// multiply the mail. Neither may fail the submission: the feedback is already saved.
    /// </summary>
    private async Task NotifyAdminsAsync(Feedback feedback)
    {
        var typeLabel = BsLabels.Label(feedback.Type);
        var title     = $"Nova povratna informacija: {typeLabel}";
        var message   = $"{feedback.Subject} — {Preview(feedback.Message)}";

        try
        {
            var adminIds = await _uow.Users.GetSystemAdminIdsAsync();
            if (adminIds.Count > 0)
            {
                await _notifications.NotifyManyInAppAsync(
                    adminIds, title, message, NotificationType.FeedbackSubmitted, feedback.Id, "Feedback");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feedback {FeedbackId} saved but in-app admin notification failed", feedback.Id);
        }

        var notifyEmail = _config["Feedback:NotifyEmail"];
        if (string.IsNullOrWhiteSpace(notifyEmail))
        {
            // Same deliberate silent skip as EmailService when SMTP is unconfigured.
            _logger.LogInformation(
                "Feedback {FeedbackId} saved; operator email skipped — Feedback:NotifyEmail is not configured",
                feedback.Id);
            return;
        }

        var severity = feedback.Severity is FeedbackSeverity s ? $" · ozbiljnost: {BsLabels.Label(s)}" : string.Empty;
        var from     = feedback.UserId is int uid ? $"korisnik #{uid}" : "nepoznat korisnik";
        var page     = feedback.PageContext is { Length: > 0 } p ? $"\nStranica: {p}" : string.Empty;

        _emailQueue.Enqueue(QueuedEmail.ForAddress(
            notifyEmail.Trim(),
            "Melarium admin",
            title,
            $"{feedback.Subject}{severity}\nPoslao: {from}{page}\n\n{feedback.Message}"));
    }

    private async Task NotifySubmitterAsync(Feedback feedback, int submitterId)
    {
        var statusLabel = BsLabels.Label(feedback.Status);
        var message = feedback.AdminResponse is { Length: > 0 } response
            ? $"\"{feedback.Subject}\" — {statusLabel}. Odgovor: {response}"
            : $"\"{feedback.Subject}\" — status je promijenjen na: {statusLabel}.";

        try
        {
            // NotifyAsync (bell + email) is right here: a user whose report was answered wants the mail.
            await _notifications.NotifyAsync(
                submitterId,
                "Odgovor na vašu povratnu informaciju",
                message,
                NotificationType.FeedbackStatusUpdated,
                feedback.Id,
                "Feedback");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "Feedback {FeedbackId} updated but submitter notification failed", feedback.Id);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private int RequireUserId() =>
        _currentUser.UserId ?? throw new ForbiddenAccessException();

    /// <summary>
    /// Loads the caller's own submission. Someone else's id yields 404, never 403 — a 403 would
    /// confirm the row exists.
    /// </summary>
    private async Task<Feedback> GetOwnOrThrowAsync(int id)
    {
        var userId = RequireUserId();

        var feedback = await _uow.Feedbacks.GetByIdAsync(id)
            ?? throw new NotFoundException("Feedback", id);

        if (feedback.UserId != userId)
            throw new NotFoundException("Feedback", id);

        return feedback;
    }

    private async Task TryDeleteBlobAsync(string storagePath)
    {
        try
        {
            await _storage.DeleteAsync(storagePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "Could not delete stored file {StoragePath} — orphaned blob left behind", storagePath);
        }
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Preview(string message) =>
        message.Length <= 140 ? message : message[..140] + "…";

    private static FeedbackDto ToDto(Feedback f) => new(
        f.Id,
        f.Type,
        BsLabels.Label(f.Type),
        f.Severity,
        f.Severity is FeedbackSeverity s ? BsLabels.Label(s) : null,
        f.Subject,
        f.Message,
        f.PageContext,
        f.ScreenshotStoragePath is not null,
        f.Status,
        BsLabels.Label(f.Status),
        f.AdminResponse,
        f.RespondedAt,
        f.CreatedAt);

    private static AdminFeedbackDto ToAdminDto(Feedback f) => new(
        f.Id,
        f.Type,
        BsLabels.Label(f.Type),
        f.Severity,
        f.Severity is FeedbackSeverity s ? BsLabels.Label(s) : null,
        f.Subject,
        f.Message,
        f.PageContext,
        f.UserAgent,
        f.ScreenshotStoragePath is not null,
        f.Status,
        BsLabels.Label(f.Status),
        f.AdminResponse,
        f.RespondedAt,
        f.CreatedAt,
        f.UserId,
        f.User is null ? null : $"{f.User.FirstName} {f.User.LastName}".Trim(),
        f.User?.Email,
        f.Organization?.Name);

    /// <summary>
    /// Determines the image type from magic bytes (JPEG / PNG / WebP), returning a seekable stream
    /// positioned at 0 alongside it (non-seekable inputs are buffered).
    /// </summary>
    private static async Task<(Stream Stream, string? ContentType)> SniffContentTypeAsync(Stream content)
    {
        var stream = content;
        if (!stream.CanSeek)
        {
            var buffered = new MemoryStream();
            await content.CopyToAsync(buffered);
            stream = buffered;
        }

        stream.Position = 0;
        var header = new byte[12];
        var read = 0;
        while (read < header.Length)
        {
            var n = await stream.ReadAsync(header.AsMemory(read, header.Length - read));
            if (n == 0) break;
            read += n;
        }
        stream.Position = 0;

        return (stream, DetectContentType(header, read));
    }

    private static string? DetectContentType(byte[] header, int length)
    {
        if (length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "image/jpeg";

        if (length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return "image/png";

        // RIFF....WEBP
        if (length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return "image/webp";

        return null;
    }
}
