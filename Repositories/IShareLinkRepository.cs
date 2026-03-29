using Groovo.Models;

namespace Groovo.Repositories;

public interface IShareLinkRepository
{
    Task<ShareLink> CreateAsync(ShareLink shareLink);
    Task<ShareLink?> GetByTokenAsync(string token);
    Task<ShareLink?> GetByIdAsync(Guid id);
    Task RevokeAsync(Guid id);
}
