using Melarium.Application.Common.Interfaces;
using Melarium.Entity.Repositories;

namespace Melarium.Entity;

/// <summary>
/// EF Core Unit of Work implementation.
/// Wraps all repositories and exposes a single SaveChangesAsync entry point,
/// ensuring all changes within a request are committed atomically.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly MelariumDbContext _context;

    // Lazy-initialised repositories — only created when first accessed
    private IOrganizationRepository? _organizations;
    private IUserRepository? _users;
    private IApiaryRepository? _apiaries;
    private IBeehiveRepository? _beehives;
    private IInspectionRepository? _inspections;
    private IInspectionPhotoRepository? _inspectionPhotos;
    private IQueenRepository? _queens;
    private IQueenEditLogRepository? _queenEditLogs;
    private ITodoRepository? _todos;
    private IDietRepository? _diets;
    private IFeedingEntryRepository? _feedingEntries;
    private IExpenseRepository? _expenses;
    private IHarvestRepository? _harvests;
    private ITreatmentRepository? _treatments;
    private ILearningTopicRepository? _learningTopics;
    private IPastureRepository? _pastures;
    private IApiaryMoveRepository? _apiaryMoves;
    private INotificationRepository? _notifications;
    private IRefreshTokenRepository? _refreshTokens;
    private IAdvisorConversationRepository? _advisorConversations;
    private ICalendarSettingsRepository? _calendarSettings;

    public UnitOfWork(MelariumDbContext context)
    {
        _context = context;
    }

    public IOrganizationRepository Organizations =>
        _organizations ??= new OrganizationRepository(_context);

    public IUserRepository Users =>
        _users ??= new UserRepository(_context);

    public IApiaryRepository Apiaries =>
        _apiaries ??= new ApiaryRepository(_context);

    public IBeehiveRepository Beehives =>
        _beehives ??= new BeehiveRepository(_context);

    public IInspectionRepository Inspections =>
        _inspections ??= new InspectionRepository(_context);

    public IInspectionPhotoRepository InspectionPhotos =>
        _inspectionPhotos ??= new InspectionPhotoRepository(_context);

    public IQueenRepository Queens =>
        _queens ??= new QueenRepository(_context);

    public IQueenEditLogRepository QueenEditLogs =>
        _queenEditLogs ??= new QueenEditLogRepository(_context);

    public ITodoRepository Todos =>
        _todos ??= new TodoRepository(_context);

    public IDietRepository Diets =>
        _diets ??= new DietRepository(_context);

    public IFeedingEntryRepository FeedingEntries =>
        _feedingEntries ??= new FeedingEntryRepository(_context);

    public IExpenseRepository Expenses =>
        _expenses ??= new ExpenseRepository(_context);

    public IHarvestRepository Harvests =>
        _harvests ??= new HarvestRepository(_context);

    public ITreatmentRepository Treatments =>
        _treatments ??= new TreatmentRepository(_context);

    public ILearningTopicRepository LearningTopics =>
        _learningTopics ??= new LearningTopicRepository(_context);

    public IPastureRepository Pastures =>
        _pastures ??= new PastureRepository(_context);

    public IApiaryMoveRepository ApiaryMoves =>
        _apiaryMoves ??= new ApiaryMoveRepository(_context);

    public INotificationRepository Notifications =>
        _notifications ??= new NotificationRepository(_context);

    public IRefreshTokenRepository RefreshTokens =>
        _refreshTokens ??= new RefreshTokenRepository(_context);

    public IAdvisorConversationRepository AdvisorConversations =>
        _advisorConversations ??= new AdvisorConversationRepository(_context);

    public ICalendarSettingsRepository CalendarSettings =>
        _calendarSettings ??= new CalendarSettingsRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);

    public void Dispose() => _context.Dispose();
}
