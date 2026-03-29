using Groovo.Models;
using System.Text.Json.Serialization;

namespace Groovo.DTOs.Responses;

public class GenericSearchResponse
{
    public List<SongSummaryResponse> Songs { get; set; } = new();
    public List<UserSummaryResponse> Users { get; set; } = new();
    public List<PlaylistSummaryResponse> Playlists { get; set; } = new();
}
