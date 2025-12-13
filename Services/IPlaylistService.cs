using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;

namespace Groovo.Services
{
    public interface IPlaylistService
    {
        Task<List<PlaylistSummaryResponse>> GetAllPlaylistsAsync(bool isAdmin);
        Task<PlaylistResponse?> GetPlaylistByIdAsync(Guid id, Guid? userId = null, bool isAdmin = false);
        Task<(PlaylistResponse? Playlist, string? ErrorMessage)> CreatePlaylistAsync(CreatePlaylistRequest request, Guid currentUserId, string userRole);
        Task<(bool Success, string? ErrorMessage)> UpdatePlaylistAsync(Guid id, UpdatePlaylistRequest request, Guid? userId = null, bool isAdmin = false);
        Task<(bool Success, string? ErrorMessage)> DeletePlaylistAsync(Guid id, Guid? userId = null, bool isAdmin = false);
        Task<List<SongSummaryResponse>?> GetSongsInPlaylistAsync(Guid id, Guid? userId = null, bool isAdmin = false);
        Task<(bool Success, string? ErrorMessage)> AddSongToPlaylistAsync(Guid playlistId, Guid songId, Guid? userId = null, bool isAdmin = false);
        Task<(bool Success, string? ErrorMessage)> RemoveSongFromPlaylistAsync(Guid playlistId, Guid songId, Guid? userId = null, bool isAdmin = false);
        Task<List<PlaylistSummaryResponse>> SearchPlaylistsAsync(string query);
    }
}
