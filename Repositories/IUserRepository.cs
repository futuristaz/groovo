using Groovo.Models;
using Groovo.DTOs;

namespace Groovo.Repositories;

public interface IUserRepository
{
    Task<User?> GetByAsync(Guid? id = null, string? email = null);
    Task<List<User>> GetByIdsAsync(List<Guid> ids);
    Task<List<User>> SearchAsync(string query, UserRole? role = null);
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid? id = null, string? email = null, Guid? excludeId = null);
}
