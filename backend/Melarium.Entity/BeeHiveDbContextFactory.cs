using Melarium.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Melarium.Entity;

/// <summary>
/// Used exclusively by the EF Core CLI tools (dotnet ef migrations add, database update, etc.)
/// at design time. Not used at runtime — the real DbContext is registered via DependencyInjection.cs.
/// </summary>
public class MelariumDbContextFactory : IDesignTimeDbContextFactory<MelariumDbContext>
{
    public MelariumDbContext CreateDbContext(string[] args)
    {
        // Walk up from Infrastructure project to find appsettings.json in the API project
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../Melarium.API");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(basePath, "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(basePath, "appsettings.Development.json"), optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found — set it in appsettings.Development.json.");

        var optionsBuilder = new DbContextOptionsBuilder<MelariumDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new MelariumDbContext(optionsBuilder.Options);
    }
}
