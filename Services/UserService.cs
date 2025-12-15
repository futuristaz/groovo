using Groovo.DTOs.Responses;
using Groovo.DTOs;
using Groovo.Repositories;

namespace Groovo.Services
{
    public class UserService : IUserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IPlaylistRepository _playlistRepository;
        private readonly ISongRepository _songRepository;

        public UserService(ILogger<UserService> logger, IUserRepository userRepository, IPlaylistRepository playlistRepository, ISongRepository songRepository)
        {
            _logger = logger;
            _userRepository = userRepository;
            _playlistRepository = playlistRepository;
            _songRepository = songRepository;
        }

        public async Task<UserResponse?> GetUserByIdAsync(Guid id)
        {
            try
            {
                var user = await _userRepository.GetByAsync(id: id);

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
                var existing = await _userRepository.GetByAsync(id: id);
                if (existing == null)
                    return false;

                existing.Name = name;
                existing.Bio = bio ?? "";
                existing.ImageUrl = imageUrl ?? "";

                await _userRepository.UpdateAsync(existing);

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
                var user = await _userRepository.GetByAsync(id: id);

                if (user == null)
                    return false;

                // Delete user - related entities (SongAuthors, PlaylistOwners, RefreshTokens) will be cascade deleted
                await _userRepository.DeleteAsync(id);

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
                var user = await _userRepository.GetByAsync(id: id);

                if (user == null || user.Role != UserRole.Author)
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
                var songs = await _songRepository.GetByAuthorAsync(authorId, includeInactive: isAdmin);

                return songs
                    .Where(s => s.IsActive || s.SongAuthors.Any(sa => sa.UserId == requestingUserId) || isAdmin)
                    .Select(s => new SongSummaryResponse(
                        s,
                        s.SongAuthors.Where(sa => sa.User.Role == UserRole.Author)
                            .Select(sa => sa.User.Name).ToList()
                    )).ToList();
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
                var playlists = await _playlistRepository.GetByUserIdAsync(userId, showFullList);

                return playlists
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
                var users = await _userRepository.SearchAsync(query, role);

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
