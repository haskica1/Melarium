namespace Melarium.Application.Common.Interfaces;

/// <summary>
/// Unit of Work pattern — coordinates repositories and persists changes atomically.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IOrganizationRepository Organizations { get; }
    IUserRepository Users { get; }
    IApiaryRepository Apiaries { get; }
    IBeehiveRepository Beehives { get; }
    IBeehiveMergeRepository BeehiveMerges { get; }
    IInspectionRepository Inspections { get; }
    IInspectionPhotoRepository InspectionPhotos { get; }
    IQueenRepository Queens { get; }
    IQueenEditLogRepository QueenEditLogs { get; }
    ITodoRepository Todos { get; }
    IDietRepository Diets { get; }
    IFeedingEntryRepository FeedingEntries { get; }
    IExpenseRepository Expenses { get; }
    IHarvestRepository Harvests { get; }
    ITreatmentRepository Treatments { get; }
    ILearningTopicRepository LearningTopics { get; }
    IPastureRepository Pastures { get; }
    IApiaryMoveRepository ApiaryMoves { get; }
    INotificationRepository Notifications { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IUserTokenRepository UserTokens { get; }
    ICalendarSettingsRepository CalendarSettings { get; }
    IFeedbackRepository Feedbacks { get; }
    IInvitationRepository Invitations { get; }
    IAiAssistantSessionRepository AiAssistantSessions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
