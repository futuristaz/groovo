using Groovo.Models;

namespace Groovo.Repositories;

public interface IPlaylistRepository
{
    Task<List<Playlist>> GetAllAsync(bool includePrivate = false);
    Task<Playlist?> GetByIdAsync(Guid id, bool includeOwners = false, bool includeSongs = false);
    Task<Playlist?> GetByIdWithSongDetailsAsync(Guid id);
    Task<List<Playlist>> GetByUserIdAsync(Guid userId, bool showFullList = false);
    Task<List<Playlist>> SearchAsync(string query);
    Task<Playlist> CreateAsync(Playlist playlist);
    Task UpdateAsync(Playlist playlist);
    Task DeleteAsync(Guid id);
    Task<int> AddSongToPlaylistAsync(Guid playlistId, Guid songId, int order);
    Task RemoveSongFromPlaylistAsync(Guid playlistId, Guid songId);
    Task<int> GetMaxSongOrderAsync(Guid playlistId);
    Task<bool> CanUserAccessPlaylistAsync(Guid playlistId, Guid userId);
    Task<Guid?> GetNextSongIdAsync(Guid playlistId, Guid currentSongId);
}
