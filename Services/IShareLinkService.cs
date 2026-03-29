using Groovo.DTOs;
using Groovo.DTOs.Responses;

namespace Groovo.Services;

public interface IShareLinkService
{
    Task<(CreateShareLinkResponse? ShareLink, string? ErrorMessage)> CreateShareLinkAsync(ShareResourceType resourceType, Guid resourceId, Guid currentUserId, string role, string baseUrl);
    Task<SharedResourceResponse?> ResolveShareLinkAsync(string token);
    Task<(bool Success, string? ErrorMessage)> RevokeShareLinkAsync(Guid shareLinkId, Guid currentUserId, string role);
}
