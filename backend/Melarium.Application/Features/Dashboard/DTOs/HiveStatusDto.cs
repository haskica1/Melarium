namespace Melarium.Application.Features.Dashboard.DTOs;

/// <summary>
/// Hives of one apiary by inspection state, under the season's threshold (SPEC-29). <c>Never</c> is a
/// hive overdue that has never been inspected; a new hive that is not yet overdue counts as in time.
/// </summary>
public record HiveStatusDto(int ApiaryId, string ApiaryName, int InTime, int Late, int Never);
