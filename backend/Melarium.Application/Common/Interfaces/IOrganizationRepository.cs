using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

/// <summary>Organization-specific data access operations.</summary>
public interface IOrganizationRepository : IRepository<Organization>
{
    Task<IEnumerable<Organization>> GetAllWithDetailsAsync();
    Task<Organization?> GetWithDetailsAsync(int id);
}
