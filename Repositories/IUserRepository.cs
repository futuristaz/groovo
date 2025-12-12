using Groovo.Models;

namespace Groovo.Repositories;

public interface IUserRepository
{
    Task<User?> GetByAsync(Guid? id = null, string? email = null);
    Task<List<User>> GetByRoleAsync(UserRole role);
    Task<List<User>> GetByIdsAsync(List<Guid> ids);
    Task<User?> GetAuthorWithSongsAsync(Guid authorId);
    Task<User?> GetUserWithPlaylistsAsync(Guid userId);
    Task<List<User>> SearchAsync(string query, UserRole? role = null);
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid? id = null, string? email = null, Guid? excludeId = null);
}
