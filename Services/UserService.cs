using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.DTOs.Responses;
using Groovo.Models;

namespace Groovo.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(ApplicationDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<UserSummaryResponse>> GetAllUsersAsync(UserRole? role = null)
        {
            try
            {
                var query = _context.Users.AsQueryable();

                if (role.HasValue)
                {
                    query = query.Where(u => u.Role == role.Value);
                }

                var users = await query
                    .OrderBy(u => u.Name)
                    .ToListAsync();

                return users.Select(u => new UserSummaryResponse(
                    u.Id,
                    u.Name,
                    u.ImageUrl ?? "",
                    u.Role
                )).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                throw;
            }
        }

        public async Task<List<UserSummaryResponse>> GetAuthorsAsync() => await GetAllUsersAsync(UserRole.Author);  

        public async Task<UserResponse?> GetUserByIdAsync(Guid id)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                    return null;

                return new UserResponse(
                    user.Id,
                    user.Name,
                    user.Bio ?? "",
                    user.ImageUrl ?? "",
                    user.Role,
                    user.CreatedAt,
                    user.UpdatedAt
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", id);
                throw;
            }
        }

        public async Task<bool> UpdateUserAsync(Guid id, string name, string? bio, string? imageUrl)
        {
            try
            {
                var existing = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
                if (existing == null)
                    return false;

                existing.Name = name;
                existing.Bio = bio ?? "";
                existing.ImageUrl = imageUrl ?? "";

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated user {UserId}: {UserName}", id, existing.Name);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.SongAuthors)
                    .Include(u => u.PlaylistOwners)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                    return false;

                // Remove all related SongAuthor entries
                if (user.SongAuthors.Any())
                {
                    _context.SongAuthors.RemoveRange(user.SongAuthors);
                }

                // Remove all related PlaylistOwner entries
                if (user.PlaylistOwners.Any())
                {
                    _context.PlaylistOwners.RemoveRange(user.PlaylistOwners);
                }

                // Remove the user itself
                _context.Users.Remove(user);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted user {UserId}: {UserName} and all related entries", id, user.Name);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                throw;
            }
        }

        public async Task<AuthorResponse?> GetAuthorByIdAsync(Guid id)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == id && u.Role == UserRole.Author)
                    .FirstOrDefaultAsync();

                if (user == null)
                    return null;

                return new AuthorResponse(
                    user.Id,
                    user.Name,
                    user.Bio,
                    user.ImageUrl
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving author {AuthorId}", id);
                throw;
            }
        }

        public async Task<List<SongSummaryResponse>> GetAuthorSongsAsync(Guid authorId, Guid? requestingUserId = null, bool isAdmin = false)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.SongAuthors)
                    .ThenInclude(sa => sa.Song)
                    .ThenInclude(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                    .FirstOrDefaultAsync(u => u.Id == authorId);

                if (user == null)
                    return new List<SongSummaryResponse>();

                var songs = user.SongAuthors
                    .Where(sa => sa.Song.IsActive || sa.UserId == requestingUserId || isAdmin)
                    .Select(sa => sa.Song)
                    .OrderByDescending(s => s.ReleaseDate)
                    .Select(s => new SongSummaryResponse(
                        s,
                        s.SongAuthors.Where(sa => sa.User.Role == UserRole.Author)
                            .Select(sa => sa.User.Name).ToList()
                    )).ToList();

                return songs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving songs for author {UserId}", authorId);
                throw;
            }
        }

        public async Task<List<PlaylistSummaryResponse>> GetUserPlaylistsAsync(Guid userId, bool showFullList)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.PlaylistOwners)
                    .ThenInclude(po => po.Playlist)
                    .ThenInclude(p => p.PlaylistSongs)
                    .Include(u => u.PlaylistOwners)
                    .ThenInclude(po => po.Playlist)
                    .ThenInclude(p => p.PlaylistOwners)
                    .ThenInclude(po => po.User)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    return new List<PlaylistSummaryResponse>();

                var playlists = user.PlaylistOwners
                    .Select(po => po.Playlist)
                    .Where(p => showFullList || p.IsPublic)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new PlaylistSummaryResponse(
                        p.Id,
                        p.Name,
                        p.Description ?? "",
                        p.Picture ?? "",
                        p.IsPublic,
                        p.IsAlbum,
                        p.TotalTime,
                        p.PlaylistSongs?.Count ?? 0
                    )).ToList();

                return playlists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving playlists for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<UserSummaryResponse>> SearchUsersAsync(string query, UserRole? role = null)
        {
            try
            {
                var queryable = _context.Users
                    .Where(u => u.Role != UserRole.Admin && (u.Name.Contains(query) ||
                           (u.Bio != null && u.Bio.Contains(query))));

                if (role.HasValue)
                {
                    queryable = queryable.Where(u => u.Role == role.Value);
                }

                var users = await queryable
                    .OrderBy(u => u.Name)
                    .ToListAsync();

                return users.Select(u => new UserSummaryResponse(
                    u.Id,
                    u.Name,
                    u.ImageUrl ?? "",
                    u.Role
                )).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with query: {Query}", query);
                throw;
            }
        }
    }
}
