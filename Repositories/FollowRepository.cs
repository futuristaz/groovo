using Groovo.Models;
using Groovo.Data.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Groovo.Repositories;

public class FollowRepository : IFollowRepository
{
    private readonly ApplicationDbContext _db;
    public FollowRepository(ApplicationDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid followerId, Guid followedId) =>
        _db.UserFollows.AnyAsync(f => f.FollowerId == followerId && f.FollowedId == followedId);

    public async Task AddAsync(UserFollow follow)
    {
        _db.UserFollows.Add(follow);
        await _db.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid followerId, Guid followedId)
    {
        var follow = await _db.UserFollows
            .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowedId == followedId);
        if (follow != null)
        {
            _db.UserFollows.Remove(follow);
            await _db.SaveChangesAsync();
        }
    }

    public Task<List<User>> GetFollowersAsync(Guid userId) =>
        _db.UserFollows
            .Where(f => f.FollowedId == userId)
            .Select(f => f.Follower)
            .ToListAsync();

    public Task<List<User>> GetFollowingAsync(Guid userId) =>
        _db.UserFollows
            .Where(f => f.FollowerId == userId)
            .Select(f => f.Followed)
            .ToListAsync();

    public Task<List<User>> GetFriendsAsync(Guid userId) =>
        _db.UserFollows
            .Where(f => f.FollowerId == userId &&
                _db.UserFollows.Any(r => r.FollowerId == f.FollowedId && r.FollowedId == userId))
            .Select(f => f.Followed)
            .ToListAsync();
}