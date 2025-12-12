using Groovo.Models;

namespace Groovo.Repositories;

public interface ISongRepository
{
    Task<List<Song>> GetAllAsync(bool includeInactive = false);
    Task<Song?> GetByIdAsync(Guid id, bool includeInactive = false, bool includeAuthors = false, bool includePlaylistSongs = false);
    Task<List<Song>> GetByAlbumAsync(Guid albumId, bool includeInactive = false);
    Task<List<Song>> GetByAuthorAsync(Guid authorId, bool includeInactive = false);
    Task<Song> CreateAsync(Song song);
    Task UpdateAsync(Song song);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<bool> ExistsByNameAndAlbumAsync(string name, Guid albumId, Guid? excludeId = null);
    Task<List<Song>> SearchAsync(string query);
    Task<List<string>> GetSongAuthorNamesAsync(Guid songId);
    Task<int> GetSongLengthAsync(Guid songId);
}
