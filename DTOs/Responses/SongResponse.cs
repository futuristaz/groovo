using Groovo.Models;

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
    string Duration, // Display as formatted string
    int DurationSeconds, // Keep raw seconds for compatibility
    int Plays,
    int Likes,
    List<AuthorResponse> Authors
) : IApiResponseValue
{
    // Constructor from entity
    public SongResponse(Song song, List<AuthorResponse> authors) : this(
        song.Id,
        song.Name,
        song.Description,
        song.ReleaseDate,
        song.Picture,
        song.Album,
        song.Genre,
        song.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
        song.AudioUrl,
        song.IsActive,
        song.CreatedAt,
        song.UpdatedAt,
        song.Duration.ToString(),
        song.Duration.TotalSeconds,
        song.Plays,
        song.Likes,
        authors
    ) { }
};