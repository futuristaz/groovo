using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;

namespace Groovo.Services.Hub;

public class PlaylistService : IPlaylistService
{
    private readonly ApplicationDbContext _dbContext;

    public PlaylistService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Check if user can access playlist: is public or is owner
    /// </summary>
    public async Task<bool> CanAccessPlaylist(Guid playlistId, Guid userId)
    {
        var playlist = await _dbContext.Playlists
            .Include(p => p.PlaylistOwners)
            .Where(p => p.Id == playlistId && (p.IsPublic || p.PlaylistOwners.Any(po => po.UserId == userId)))
            .FirstOrDefaultAsync();

        return playlist != null;
    }

    /// <summary>
    /// Get the next song ID in the playlist based on order
    /// </summary>
    /// <param name="playlistId">The playlist ID</param>
    /// <param name="currentSongId">The current song ID</param>
    public async Task<Guid?> GetNextSongId(Guid playlistId, Guid currentSongId)
    {
        var currentSongOrder = await _dbContext.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlistId && ps.SongId == currentSongId && ps.Song.IsActive)
            .Select(ps => ps.Order)
            .FirstOrDefaultAsync();

        if (currentSongOrder == 0)
        {
            return null;
        }

        var nextSong = await _dbContext.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlistId && ps.Order > currentSongOrder && ps.Song.IsActive)
            .OrderBy(ps => ps.Order)
            .Select(ps => ps.SongId)
            .FirstOrDefaultAsync();

        return nextSong == Guid.Empty ? null : nextSong;
    }

    /// <summary>
    /// Get the length of a song in seconds
    /// </summary>
    /// <param name="songId">The song ID</param>
    public async Task<int> GetSongLength(Guid songId)
    {
        var songLength = await _dbContext.Songs
            .Where(s => s.Id == songId && s.IsActive)
            .Select(s => s.Length)
            .FirstOrDefaultAsync();

        return songLength;
    }
}
