using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.Models;
using Groovo.DTOs;
using FuzzySharp;

namespace Groovo.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByAsync(Guid? id = null, string? email = null)
    {
        if (id == null && string.IsNullOrEmpty(email))
            return null;

        var query = _context.Users.AsQueryable();

        if (id.HasValue)
        {
            query = query.Where(u => u.Id == id.Value);
        }

        if (!string.IsNullOrEmpty(email))
        {
            query = query.Where(u => u.Email == email);
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<List<User>> GetByIdsAsync(List<Guid> ids)
    {
        return await _context.Users
            .Where(u => ids.Contains(u.Id))
            .ToListAsync();
    }

    public async Task<List<User>> SearchAsync(string query, UserRole? role = null)
    {
        var user_query = _context.Users
            .Where(u => u.Role != UserRole.Admin);

        if (role.HasValue)
        {
            user_query = user_query.Where(u => u.Role == role.Value);
        }

        var users = await user_query.ToListAsync();

        return users
            .Select(u => new
            {
                User = u,
                Score = new[]
                {
                    Fuzz.PartialRatio(query, u.Name),
                    Fuzz.PartialRatio(query, u.Bio)
                }.Max()
            })
            .Where(x => x.Score >= 60)
            .OrderByDescending(x => x.Score)
            .Select(x => x.User)
            .ToList();
    }

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await GetByAsync(id: id);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(Guid? id = null, string? email = null, Guid? excludeId = null)
    {
        if (id == null && string.IsNullOrEmpty(email))
            return false;

        var query = _context.Users.AsQueryable();

        if (id.HasValue)
        {
            query = query.Where(u => u.Id == id.Value);
        }

        if (!string.IsNullOrEmpty(email))
        {
            query = query.Where(u => u.Email == email);
        }

        if (excludeId.HasValue)
        {
            query = query.Where(u => u.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}
