using Melarium.Application.Features.Pastures.DTOs;

namespace Melarium.Application.Features.Pastures;

public interface IApiaryMoveService
{
    /// <summary>Move history, newest first — anyone who can view the apiary.</summary>
    Task<IEnumerable<ApiaryMoveDto>> GetByApiaryAsync(int apiaryId);

    /// <summary>
    /// Records a move: resolves FromPasture server-side, sets the apiary's current pasture, and
    /// snapshots the pasture coordinates into the apiary (weather/map follow automatically).
    /// </summary>
    Task<ApiaryMoveDto> CreateAsync(int apiaryId, CreateApiaryMoveDto dto);

    /// <summary>Mistake correction: only the latest move can be deleted; reverts pasture + coordinates.</summary>
    Task DeleteAsync(int apiaryId, int moveId);

    /// <summary>
    /// Moves the apiary back to its matična lokacija: records a move with a null target and restores
    /// the captured Home coordinates. Requires a known Home location and that the apiary is currently
    /// away (on a pasture).
    /// </summary>
    Task<ApiaryMoveDto> ReturnHomeAsync(int apiaryId);

    /// <summary>
    /// Declares or corrects the apiary's matična lokacija without recording a move — for apiaries that
    /// already relocated before Home tracking existed (their true original location is unrecoverable).
    /// </summary>
    Task SetHomeLocationAsync(int apiaryId, double latitude, double longitude);
}
