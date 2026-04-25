using Groovo.DTOs.Responses;
using Groovo.Models;
using Groovo.Repositories;

namespace Groovo.Services;

public class FollowService : IFollowService
{
    private readonly ILogger<FollowService> _logger;
    private readonly IFollowRepository _followRepository;
    private readonly IUserRepository _userRepository;

    public FollowService(
        ILogger<FollowService> logger,
        IFollowRepository followRepository,
        IUserRepository userRepository)
    {
        _logger = logger;
        _followRepository = followRepository;
        _userRepository = userRepository;
    }

    public async Task<bool> FollowAsync(Guid currentUserId, Guid targetUserId)
    {
        try
        {
            if (currentUserId == targetUserId)
                return false;

            var targetUser = await _userRepository.GetByAsync(id: targetUserId);
            if (targetUser is null)
                return false;

            var alreadyFollowing = await _followRepository.ExistsAsync(currentUserId, targetUserId);
            if (alreadyFollowing)
                return false;

            await _followRepository.AddAsync(new UserFollow
            {
                FollowerId = currentUserId,
                FollowedId = targetUserId,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogInformation(
                "User {FollowerId} followed user {FollowedId}",
                currentUserId, targetUserId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error following user {TargetUserId}", targetUserId);
            throw;
        }
    }

    public async Task<bool> UnfollowAsync(Guid currentUserId, Guid targetUserId)
    {
        try
        {
            if (currentUserId == targetUserId)
                return false;

            var isFollowing = await _followRepository.ExistsAsync(currentUserId, targetUserId);
            if (!isFollowing)
                return false;

            await _followRepository.RemoveAsync(currentUserId, targetUserId);

            _logger.LogInformation(
                "User {FollowerId} unfollowed user {FollowedId}",
                currentUserId, targetUserId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unfollowing user {TargetUserId}", targetUserId);
            throw;
        }
    }

    public async Task<List<FollowUserResponse>?> GetFollowersAsync(Guid userId)
    {
        try
        {
            var userExists = await _userRepository.ExistsAsync(id: userId);
            if (!userExists)
                return null;

            var followers = await _followRepository.GetFollowersAsync(userId);

            _logger.LogInformation(
                "Retrieved {Count} followers for user {UserId}",
                followers.Count, userId);

            return followers.Select(MapToResponse).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving followers for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<FollowUserResponse>?> GetFollowingAsync(Guid userId)
    {
        try
        {
            var userExists = await _userRepository.ExistsAsync(id: userId);
            if (!userExists)
                return null;

            var following = await _followRepository.GetFollowingAsync(userId);

            _logger.LogInformation(
                "Retrieved {Count} following for user {UserId}",
                following.Count, userId);

            return following.Select(MapToResponse).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving following for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<FollowUserResponse>?> GetFriendsAsync(Guid userId)
    {
        try
        {
            var userExists = await _userRepository.ExistsAsync(id: userId);
            if (!userExists)
                return null;

            var friends = await _followRepository.GetFriendsAsync(userId);

            _logger.LogInformation(
                "Retrieved {Count} friends for user {UserId}",
                friends.Count, userId);

            return friends.Select(MapToResponse).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving friends for user {UserId}", userId);
            throw;
        }
    }

    public async Task<RelationshipStatusResponse?> GetRelationshipStatusAsync(Guid currentUserId, Guid targetUserId)
    {
        try
        {
            if (currentUserId == targetUserId)
                return null;

            var targetExists = await _userRepository.ExistsAsync(id: targetUserId);
            if (!targetExists)
                return null;

            var iFollowThem = await _followRepository.ExistsAsync(currentUserId, targetUserId);
            var theyFollowMe = await _followRepository.ExistsAsync(targetUserId, currentUserId);

            _logger.LogInformation(
                "Relationship status between {CurrentUserId} and {TargetUserId}: IFollowThem={IFollowThem}, TheyFollowMe={TheyFollowMe}",
                currentUserId, targetUserId, iFollowThem, theyFollowMe);

            return new RelationshipStatusResponse
            {
                IFollowThem = iFollowThem,
                TheyFollowMe = theyFollowMe,
                AreFriends = iFollowThem && theyFollowMe
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving relationship status with user {TargetUserId}", targetUserId);
            throw;
        }
    }

    private static FollowUserResponse MapToResponse(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        ImageUrl = user.ImageUrl
    };
}