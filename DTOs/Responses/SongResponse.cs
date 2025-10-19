namespace Groovo.DTOs.Responses;

public record SongResponse(
    Guid Id,
    string Name,
    string Description,
    DateTime ReleaseDate,
    string Picture,
    Guid Album,
    string Genre,
    List<string> Tags,
    string AudioUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Length,
    int Plays,
    int Likes,
    List<AuthorResponse> Authors
);