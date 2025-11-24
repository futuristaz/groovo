namespace Groovo.Services.Hub;

public interface IPlaylistService
{
    Task<bool> CanAccessPlaylist(Guid playlistId, Guid userId);
    Task<Guid?> GetNextSongId(Guid playlistId, Guid currentSongId);
    Task<int> GetSongLength(Guid songId);
}