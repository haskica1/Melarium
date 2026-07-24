using Melarium.Application.Features.Beehives.DTOs;

namespace Melarium.Application.Features.Apiaries.DTOs;

/// <summary>Full apiary representation including its beehives.</summary>
public class ApiaryDetailDto : ApiaryDto
{
    public IEnumerable<BeehiveDto> Beehives { get; set; } = new List<BeehiveDto>();
}
