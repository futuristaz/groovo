using Groovo.DTOs.Responses;

namespace Groovo.Services;

public interface IFollowService
{
    Task<bool> FollowAsync(Guid currentUserId, Guid targetUserId);
    Task<bool> UnfollowAsync(Guid currentUserId, Guid targetUserId);
    Task<List<FollowUserResponse>?> GetFollowersAsync(Guid userId);
    Task<List<FollowUserResponse>?> GetFollowingAsync(Guid userId);
    Task<List<FollowUserResponse>?> GetFriendsAsync(Guid userId);
    Task<RelationshipStatusResponse?> GetRelationshipStatusAsync(Guid currentUserId, Guid targetUserId);
}