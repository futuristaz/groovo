using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.Models;
using Groovo.DTOs;

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
        var queryable = _context.Users
            .Where(u => u.Role != UserRole.Admin && (
                u.Name.Contains(query) ||
                (u.Bio != null && u.Bio.Contains(query))
            ));

        if (role.HasValue)
        {
            queryable = queryable.Where(u => u.Role == role.Value);
        }

        return await queryable
            .OrderBy(u => u.Name)
            .ToListAsync();
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
