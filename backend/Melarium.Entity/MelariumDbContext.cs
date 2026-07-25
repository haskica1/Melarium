using Melarium.Domain.Entities;
using Melarium.Entity.Configurations;
using Melarium.Entity.Seed;
using Microsoft.EntityFrameworkCore;

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
    public DbSet<FeedingEntry> FeedingEntries => Set<FeedingEntry>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseItem> ExpenseItems => Set<ExpenseItem>();
    public DbSet<Harvest> Harvests => Set<Harvest>();
    public DbSet<HarvestEntry> HarvestEntries => Set<HarvestEntry>();
    public DbSet<Treatment> Treatments => Set<Treatment>();
    public DbSet<TreatmentEntry> TreatmentEntries => Set<TreatmentEntry>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MelariumDbContext).Assembly);

        // Seed initial data
        DataSeeder.Seed(modelBuilder);
    }

    /// <summary>
    /// Automatically sets UpdatedAt on modified entities before saving, and normalises
    /// any DateTime.Unspecified values to UTC so Npgsql accepts them as timestamptz.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.State == EntityState.Modified &&
                entry.Entity is Melarium.Domain.Common.BaseEntity entity)
            {
                entity.UpdatedAt = DateTime.UtcNow;
            }

            // Npgsql 6+ requires DateTimeKind.Utc for timestamptz columns.
            // User-supplied dates arrive as Kind=Unspecified; convert them here
            // so callers don't need to remember to do it everywhere.
            foreach (var prop in entry.Properties)
            {
                if (prop.CurrentValue is DateTime dt && dt.Kind == DateTimeKind.Unspecified)
                    prop.CurrentValue = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
