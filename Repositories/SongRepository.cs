using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.Models;

namespace Groovo.Repositories;

public class SongRepository : ISongRepository
{
    private readonly ApplicationDbContext _context;

    public SongRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Song?> GetByIdAsync(Guid id, bool includeInactive = false, bool includeAuthors = false, bool includePlaylistSongs = false)
    {
        var query = _context.Songs.AsQueryable();

        if (includeAuthors)
        {
            query = query.Include(s => s.SongAuthors)
                         .ThenInclude(sa => sa.User);
        }

        if (includePlaylistSongs)
        {
            query = query.Include(s => s.PlaylistSongs);
        }

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query.FirstOrDefaultAsync(s => s.Id == id);
    }
    
    public async Task<List<Song>> GetByAuthorAsync(Guid authorId, bool includeInactive = false)
    {
        var query = _context.Songs
            .Include(s => s.SongAuthors)
            .ThenInclude(sa => sa.User)
            .Where(s => s.SongAuthors.Any(sa => sa.UserId == authorId));

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query
            .OrderByDescending(s => s.ReleaseDate)
            .ToListAsync();
    }

    public async Task<Song> CreateAsync(Song song)
    {
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();
        return song;
    }

    public async Task UpdateAsync(Song song)
    {
        _context.Songs.Update(song);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var song = await GetByIdAsync(id, includeInactive: true);
        if (song != null)
        {
            _context.Songs.Remove(song);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsByNameAndAlbumAsync(string name, Guid albumId, Guid? excludeId = null)
    {
        var query = _context.Songs
            .Where(s => s.Name == name && s.Album == albumId);

        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<List<Song>> SearchAsync(string query)
    {
        return await _context.Songs
            .Include(s => s.SongAuthors)
            .ThenInclude(sa => sa.User)
            .Where(s => s.IsActive && (
                s.Name.Contains(query) ||
                s.Genre.Contains(query) ||
                s.SongAuthors.Any(sa => sa.User.Name.Contains(query))
            ))
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<List<string>> GetSongAuthorNamesAsync(Guid songId)
    {
        return await _context.SongAuthors
            .Where(sa => sa.SongId == songId)
            .Include(sa => sa.User)
            .Where(sa => sa.User.Role == UserRole.Author)
            .Select(sa => sa.User.Name)
            .ToListAsync();
    }

    public async Task<int> GetSongLengthAsync(Guid songId)
    {
        return await _context.Songs
            .Where(s => s.Id == songId && s.IsActive)
            .Select(s => s.Length)
            .FirstOrDefaultAsync();
    }
}
