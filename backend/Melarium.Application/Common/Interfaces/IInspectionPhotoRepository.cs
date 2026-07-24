using Melarium.Domain.Entities;

namespace Melarium.Application.Common.Interfaces;

public interface IInspectionPhotoRepository : IRepository<InspectionPhoto>
{
    Task<IEnumerable<InspectionPhoto>> GetByInspectionIdAsync(int inspectionId);
    Task<int> CountByInspectionAsync(int inspectionId);
}
