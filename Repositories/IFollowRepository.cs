using Groovo.Models;

namespace Groovo.Repositories;
public interface IFollowRepository
{
    Task<bool> ExistsAsync(Guid followerId, Guid followedId);
    Task AddAsync(UserFollow follow);
    Task RemoveAsync(Guid followerId, Guid followedId);
    Task<List<User>> GetFollowersAsync(Guid userId);
    Task<List<User>> GetFollowingAsync(Guid userId);
    Task<List<User>> GetFriendsAsync(Guid userId);
}