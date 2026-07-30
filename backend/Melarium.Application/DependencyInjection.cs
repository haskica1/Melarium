using Melarium.Application.Common.Security;
using Melarium.Application.Features.Apiaries.Validators;
using Melarium.Application.Common.Services;
using Melarium.Application.Features.Admin;
using Melarium.Application.Features.Advisor;
using Melarium.Application.Features.Alerts;
using Melarium.Application.Features.Apiaries;
using Melarium.Application.Features.OrgManagement;
using Melarium.Application.Features.Auth;
using Melarium.Application.Features.Calendar;
using Melarium.Application.Features.Notifications;
using Melarium.Application.Features.Profile;
using Melarium.Application.Features.Stats;
using Melarium.Application.Features.Beehives;
using Melarium.Application.Features.Diets;
using Melarium.Application.Features.Expenses;
using Melarium.Application.Features.Feedbacks;
using Melarium.Application.Features.Harvests;
using Melarium.Application.Features.Inspections;
using Melarium.Application.Features.Learning;
using Melarium.Application.Features.Pastures;
using Melarium.Application.Features.Queens;
using Melarium.Application.Features.Reminders;
using Melarium.Application.Features.Todos;
using Melarium.Application.Features.Treatments;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Melarium.Application;

/// <summary>
/// Extension method to register all Application-layer services in the DI container.
/// Called from the API's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // AutoMapper — scans this assembly for all per-feature Profile subclasses
        services.AddAutoMapper(typeof(DependencyInjection).Assembly);

        // FluentValidation — registers all validators in this assembly
        services.AddValidatorsFromAssemblyContaining<CreateApiaryValidator>();

        // Cross-cutting authorization — single source of truth for tenant/resource access
        services.AddScoped<IAccessGuard, AccessGuard>();

        // Subscription-plan enforcement (SPEC-09) — single source of truth for plan gates
        services.AddScoped<IPlanGuard, PlanGuard>();

        // Session termination — used wherever a credential or a JWT-carried privilege changes
        services.AddScoped<ISessionRevoker, SessionRevoker>();

        // Application services
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ICalendarService, CalendarService>();

        // Calendar integration (SPEC-11): shared obligation aggregation + private ICS feed + 08:00 agenda
        services.AddScoped<ICalendarAccessResolver, CalendarAccessResolver>();
        services.AddScoped<ICalendarObligationService, CalendarObligationService>();
        services.AddScoped<ICalendarFeedService, CalendarFeedService>();
        services.AddScoped<IDailyAgendaService, DailyAgendaService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IOrgManagementService, OrgManagementService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IApiaryService, ApiaryService>();
        services.AddScoped<IBeehiveService, BeehiveService>();
        services.AddScoped<IInspectionService, InspectionService>();
        services.AddScoped<IInspectionPhotoService, InspectionPhotoService>();
        services.AddScoped<IQueenService, QueenService>();
        services.AddScoped<ITodoService, TodoService>();
        services.AddScoped<IDietService, DietService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IHarvestService, HarvestService>();
        services.AddScoped<ITreatmentService, TreatmentService>();
        services.AddScoped<ILearningTopicService, LearningTopicService>();
        services.AddScoped<IPastureService, PastureService>();
        services.AddScoped<IApiaryMoveService, ApiaryMoveService>();
        services.AddScoped<IAdvisorService, AdvisorService>();
        services.AddScoped<IAlertRuleService, AlertRuleService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddSingleton<IQrCodeService, QrCodeService>();

        return services;
    }
}
