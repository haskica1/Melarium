using Melarium.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Melarium.Entity;

/// <summary>
/// Registers the persistence layer: the EF Core <see cref="MelariumDbContext"/>, the Unit of Work,
/// and (transitively) all repositories. The data project owns the database connection and migrations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddEntity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<MelariumDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(MelariumDbContext).Assembly.FullName)
            ));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
