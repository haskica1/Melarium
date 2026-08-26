using Melarium.Domain.Entities;
using Melarium.Entity.Configurations;
using Melarium.Entity.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Melarium.Entity;

/// <summary>
/// Main EF Core database context for the Melarium application.
/// Each DbSet corresponds to a database table managed by EF Core migrations.
/// </summary>
public class MelariumDbContext : DbContext
{
    public MelariumDbContext(DbContextOptions<MelariumDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Apiary> Apiaries => Set<Apiary>();
    public DbSet<Beehive> Beehives => Set<Beehive>();
    public DbSet<UserBeehive> UserBeehives => Set<UserBeehive>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<InspectionPhoto> InspectionPhotos => Set<InspectionPhoto>();
    public DbSet<Queen> Queens => Set<Queen>();
    public DbSet<QueenEditLog> QueenEditLogs => Set<QueenEditLog>();
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<Diet> Diets => Set<Diet>();
    public DbSet<DietBeehive> DietBeehives => Set<DietBeehive>();
    public DbSet<FeedingEntry> FeedingEntries => Set<FeedingEntry>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseItem> ExpenseItems => Set<ExpenseItem>();
    public DbSet<Harvest> Harvests => Set<Harvest>();
    public DbSet<HarvestEntry> HarvestEntries => Set<HarvestEntry>();
    public DbSet<Treatment> Treatments => Set<Treatment>();
    public DbSet<TreatmentEntry> TreatmentEntries => Set<TreatmentEntry>();
    public DbSet<TreatmentRound> TreatmentRounds => Set<TreatmentRound>();
    public DbSet<LearningTopic> LearningTopics => Set<LearningTopic>();
    public DbSet<LearningTopicRead> LearningTopicReads => Set<LearningTopicRead>();
    public DbSet<Pasture> Pastures => Set<Pasture>();
    public DbSet<ApiaryMove> ApiaryMoves => Set<ApiaryMove>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<AdvisorConversation> AdvisorConversations => Set<AdvisorConversation>();
    public DbSet<AdvisorMessage> AdvisorMessages => Set<AdvisorMessage>();
    public DbSet<CalendarSettings> CalendarSettings => Set<CalendarSettings>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<AiAssistantSession> AiAssistantSessions => Set<AiAssistantSession>();
    public DbSet<AiAssistantTurn> AiAssistantTurns => Set<AiAssistantTurn>();
    public DbSet<AiAssistantAction> AiAssistantActions => Set<AiAssistantAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MelariumDbContext).Assembly);

        // Every DateTime in the model is written as UTC, enforced here rather than by a caller
        // (ADR-037). Npgsql rejects any Kind other than Utc on a timestamptz column, and the
        // previous defence — a ChangeTracker sweep in SaveChangesAsync — only reached entries that
        // were already Added or Modified at the moment it ran, so a value that arrived later was
        // written unfixed. A converter runs at the persistence boundary, which nothing can bypass.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(UtcDateTime);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(NullableUtcDateTime);
            }
        }

        // Seed initial data
        DataSeeder.Seed(modelBuilder);
    }

    /// <summary>
    /// Unspecified is *reinterpreted* as UTC — it is how a naive calendar date has always been
    /// stored here, and changing it would move every existing timestamp. Local is genuinely
    /// converted, because reinterpreting it would record the wrong instant.
    /// </summary>
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc   => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _                  => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    private static readonly ValueConverter<DateTime, DateTime> UtcDateTime =
        new(v => ToUtc(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcDateTime =
        new(v => v.HasValue ? ToUtc(v.Value) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    /// <summary>
    /// Automatically sets UpdatedAt on modified entities before saving. UTC normalisation used to
    /// live here too; it is now a model-wide value converter in <see cref="OnModelCreating"/>, which
    /// cannot be outrun by an entity that enters the change tracker after this method has looked
    /// (ADR-037). Do not re-add a sweep here — one mechanism, at the persistence boundary.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified))
        {
            if (entry.Entity is Melarium.Domain.Common.BaseEntity entity)
                entity.UpdatedAt = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
