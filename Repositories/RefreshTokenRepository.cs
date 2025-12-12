using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.Models;

namespace Groovo.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _context;

    public RefreshTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, bool includeUser = false, bool validOnly = false)
    {
        var query = _context.RefreshTokens.AsQueryable();

        if (includeUser)
        {
            query = query.Include(rt => rt.User);
        }

        query = query.Where(rt => rt.Token == token);

        if (validOnly)
        {
            query = query.Where(rt => !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow);
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<RefreshToken> CreateAsync(RefreshToken refreshToken)
    {
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();
        return refreshToken;
    }

    public async Task UpdateAsync(RefreshToken refreshToken)
    {
        _context.RefreshTokens.Update(refreshToken);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var token = await _context.RefreshTokens.FindAsync(id);
        if (token != null)
        {
            _context.RefreshTokens.Remove(token);
            await _context.SaveChangesAsync();
        }
    }
}
