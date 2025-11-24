using Groovo.Models;

namespace Groovo.DTOs.Responses;

public record SongSummaryResponse(
    Guid Id,
    string Name,
    string Genre,
    DateTime ReleaseDate,
    string Picture,
    string Duration, // Display as formatted string
    int DurationSeconds, // Keep raw seconds for compatibility
    int Plays,
    int Likes,
    List<string> AuthorNames
) : IApiResponseValue
{
    // Constructor from entity
    public SongSummaryResponse(Song song, List<string> authorNames) : this(
        song.Id,
        song.Name,
        song.Genre,
        song.ReleaseDate,
        song.Picture,
        song.Duration.ToString(),
        song.Duration.TotalSeconds,
        song.Plays,
        song.Likes,
        authorNames
    ) { }
};