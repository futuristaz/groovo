namespace Groovo.DTOs.Responses;

public record PlaylistResponse(
    Guid Id,
    string Name,
    string Description,
    string Picture,
    bool IsActive,
    bool IsPublic,
    bool IsAlbum,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int TotalTime,
    int SongCount,
    List<UserSummaryResponse> Owners
) : IApiResponseValue;