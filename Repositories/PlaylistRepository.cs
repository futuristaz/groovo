using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.Models;

namespace Groovo.Repositories;

public class PlaylistRepository : IPlaylistRepository
{
    private readonly ApplicationDbContext _context;

    public PlaylistRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Playlist>> GetAllAsync(bool includePrivate = false)
    {
        var query = _context.Playlists
            .Include(p => p.PlaylistSongs)
            .AsQueryable();

        if (!includePrivate)
        {
            query = query.Where(p => p.IsPublic);
        }

        return await query
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Playlist?> GetByIdAsync(Guid id, bool includeOwners = false, bool includeSongs = false)
    {
        var query = _context.Playlists.AsQueryable();

        if (includeOwners)
        {
            query = query.Include(p => p.PlaylistOwners)
                         .ThenInclude(po => po.User);
        }

        if (includeSongs)
        {
            query = query.Include(p => p.PlaylistSongs);
        }

        return await query.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Playlist?> GetByIdWithSongDetailsAsync(Guid id)
    {
        return await _context.Playlists
            .Include(p => p.PlaylistSongs)
            .ThenInclude(ps => ps.Song)
            .ThenInclude(s => s.SongAuthors)
            .ThenInclude(sa => sa.User)
            .Include(p => p.PlaylistOwners)
            .ThenInclude(po => po.User)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Playlist>> GetByUserIdAsync(Guid userId, bool showFullList = false)
    {
        var query = _context.Playlists
            .Include(p => p.PlaylistOwners)
            .AsQueryable();

        if (showFullList)
        {
            query = query.Where(p => p.PlaylistOwners.Any(po => po.UserId == userId));
        }
        else
        {
            query = query.Where(p => p.IsPublic && p.PlaylistOwners.Any(po => po.UserId == userId));
        }

        return await query
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Playlist>> SearchAsync(string query)
    {
        return await _context.Playlists
            .Include(p => p.PlaylistSongs)
            .Where(p => p.IsPublic && (
                p.Name.Contains(query) ||
                (p.Description != null && p.Description.Contains(query))
            ))
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Playlist> CreateAsync(Playlist playlist)
    {
        _context.Playlists.Add(playlist);
        await _context.SaveChangesAsync();
        return playlist;
    }

    public async Task UpdateAsync(Playlist playlist)
    {
        _context.Playlists.Update(playlist);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var playlist = await GetByIdAsync(id);
        if (playlist != null)
        {
            _context.Playlists.Remove(playlist);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> AddSongToPlaylistAsync(Guid playlistId, Guid songId, int order)
    {
        var playlistSong = new PlaylistSong
        {
            PlaylistId = playlistId,
            SongId = songId,
            Order = order
        };

        _context.PlaylistSongs.Add(playlistSong);
        await _context.SaveChangesAsync();

        return order;
    }

    public async Task RemoveSongFromPlaylistAsync(Guid playlistId, Guid songId)
    {
        var playlistSong = await _context.PlaylistSongs
            .FirstOrDefaultAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId);

        if (playlistSong != null)
        {
            _context.PlaylistSongs.Remove(playlistSong);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetMaxSongOrderAsync(Guid playlistId)
    {
        return await _context.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlistId)
            .MaxAsync(ps => (int?)ps.Order) ?? -1;
    }

    public async Task<bool> CanUserAccessPlaylistAsync(Guid playlistId, Guid userId)
    {
        return await _context.Playlists
            .Include(p => p.PlaylistOwners)
            .Where(p => p.Id == playlistId && (p.IsPublic || p.PlaylistOwners.Any(po => po.UserId == userId)))
            .AnyAsync();
    }

    public async Task<Guid?> GetNextSongIdAsync(Guid playlistId, Guid currentSongId)
    {
        var currentSongOrder = await _context.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlistId && ps.SongId == currentSongId)
            .Include(ps => ps.Song)
            .Where(ps => ps.Song.IsActive)
            .Select(ps => (int?)ps.Order)
            .FirstOrDefaultAsync();

        if (currentSongOrder == null)
        {
            return null;
        }

        var nextSongId = await _context.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlistId && ps.Order > currentSongOrder)
            .Include(ps => ps.Song)
            .Where(ps => ps.Song.IsActive)
            .OrderBy(ps => ps.Order)
            .Select(ps => ps.SongId)
            .FirstOrDefaultAsync();

        return nextSongId == Guid.Empty ? null : nextSongId;
    }

    public async Task<List<Guid>> GetAllActiveSongIdsInOrderAsync(Guid playlistId)
    {
        return await _context.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlistId)
            .Include(ps => ps.Song)
            .Where(ps => ps.Song.IsActive)
            .OrderBy(ps => ps.Order)
            .Select(ps => ps.SongId)
            .ToListAsync();
    }
}
