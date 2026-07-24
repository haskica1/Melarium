using Melarium.Application.Features.Inspections.DTOs;

namespace Melarium.Application.Features.Inspections;

public interface IVoiceParsingService
{
    Task<ParseVoiceResult> ParseAsync(Stream audioStream, string fileName);
}
