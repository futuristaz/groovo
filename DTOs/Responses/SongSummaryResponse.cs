namespace Groovo.DTOs.Responses;

public record SongSummaryResponse(
    Guid Id,
    string Name,
    string Genre,
    DateTime ReleaseDate,
    string Picture,
    int Length,
    int Plays,
    int Likes,
    List<string> AuthorNames
);