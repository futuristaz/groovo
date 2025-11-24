namespace Groovo.DTOs.Responses;

public record PlaylistSummaryResponse(
    Guid Id,
    string Name,
    string Description,
    string Picture,
    bool IsPublic,
    bool IsAlbum,
    int TotalTime,
    int SongCount
) : IApiResponseValue;