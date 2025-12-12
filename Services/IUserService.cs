using Groovo.DTOs.Responses;
using Groovo.Models;

namespace Groovo.Services
{
    public interface IUserService
    {
        Task<UserResponse?> GetUserByIdAsync(Guid id);
        Task<bool> UpdateUserAsync(Guid id, string name, string? bio, string? imageUrl);
        Task<bool> DeleteUserAsync(Guid id);
        Task<AuthorResponse?> GetAuthorByIdAsync(Guid id);
        Task<List<SongSummaryResponse>> GetAuthorSongsAsync(Guid authorId, Guid? requestingUserId = null, bool isAdmin = false);
        Task<List<PlaylistSummaryResponse>> GetUserPlaylistsAsync(Guid userId, bool showFullList);
        Task<List<UserSummaryResponse>> SearchUsersAsync(string query, UserRole? role = null);
    }
}
