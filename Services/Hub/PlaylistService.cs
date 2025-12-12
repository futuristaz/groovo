using Groovo.Repositories;

namespace Groovo.Services.Hub;

public class PlaylistService : IPlaylistService
{
    private readonly IPlaylistRepository _playlistRepository;
    private readonly ISongRepository _songRepository;

    public PlaylistService(IPlaylistRepository playlistRepository, ISongRepository songRepository)
    {
        _playlistRepository = playlistRepository;
        _songRepository = songRepository;
    }

    /// <summary>
    /// Check if user can access playlist: is public or is owner
    /// </summary>
    public async Task<bool> CanAccessPlaylist(Guid playlistId, Guid userId)
    {
        return await _playlistRepository.CanUserAccessPlaylistAsync(playlistId, userId);
    }

    /// <summary>
    /// Get the next song ID in the playlist based on order
    /// </summary>
    /// <param name="playlistId">The playlist ID</param>
    /// <param name="currentSongId">The current song ID</param>
    public async Task<Guid?> GetNextSongId(Guid playlistId, Guid currentSongId)
    {
        return await _playlistRepository.GetNextSongIdAsync(playlistId, currentSongId);
    }

    /// <summary>
    /// Get the length of a song in seconds
    /// </summary>
    /// <param name="songId">The song ID</param>
    public async Task<int> GetSongLength(Guid songId)
    {
        return await _songRepository.GetSongLengthAsync(songId);
    }
}
