using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.Models;

namespace Groovo.Repositories;

public class ShareLinkRepository : IShareLinkRepository
{
    private readonly ApplicationDbContext _context;

    public ShareLinkRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ShareLink> CreateAsync(ShareLink shareLink)
    {
        _context.ShareLinks.Add(shareLink);
        await _context.SaveChangesAsync();
        return shareLink;
    }

    public async Task<ShareLink?> GetByTokenAsync(string token)
    {
        return await _context.ShareLinks
            .FirstOrDefaultAsync(sl => sl.Token == token);
    }

    public async Task<ShareLink?> GetByIdAsync(Guid id)
    {
        return await _context.ShareLinks
            .FirstOrDefaultAsync(sl => sl.Id == id);
    }

    public async Task RevokeAsync(Guid id)
    {
        var link = await GetByIdAsync(id);
        if (link != null)
        {
            link.IsRevoked = true;
            await _context.SaveChangesAsync();
        }
    }
}
