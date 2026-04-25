using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Groovo.DTOs.Responses;
using Groovo.Services;
using System.Security.Claims;

namespace Groovo.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/users")]
    [ApiVersion("1.0")]
    public class FollowController : ControllerBase
    {
        private readonly IFollowService _followService;
        private readonly ILogger<FollowController> _logger;

        public FollowController(IFollowService followService, ILogger<FollowController> logger)
        {
            _followService = followService;
            _logger = logger;
        }

        /// <summary>
        /// POST: /api/v1/users/{id}/follow
        /// Follow another user. If they already follow you back, you become friends.
        /// </summary>
        [HttpPost("{id:guid}/follow")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult> Follow(Guid id)
        {
            var currentUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId)
                ? parsedId : (Guid?)null;

            if (currentUserId == null)
                return Unauthorized();

            if (currentUserId == id)
                return BadRequest("You cannot follow yourself.");

            try
            {
                var success = await _followService.FollowAsync(currentUserId.Value, id);

                if (!success)
                    return NotFound($"User with ID {id} not found.");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error following user {TargetUserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// DELETE: /api/v1/users/{id}/follow
        /// Unfollow a user. If you were friends, you revert to them just following you.
        /// </summary>
        [HttpDelete("{id:guid}/follow")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult> Unfollow(Guid id)
        {
            var currentUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId)
                ? parsedId : (Guid?)null;

            if (currentUserId == null)
                return Unauthorized();

            if (currentUserId == id)
                return BadRequest("You cannot unfollow yourself.");

            try
            {
                var success = await _followService.UnfollowAsync(currentUserId.Value, id);

                if (!success)
                    return BadRequest("You are not following this user.");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unfollowing user {TargetUserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// GET: /api/v1/users/{id}/followers
        /// Get a list of users who follow the specified user.
        /// </summary>
        [HttpGet("{id:guid}/followers")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult<IEnumerable<FollowUserResponse>>> GetFollowers(Guid id)
        {
            try
            {
                var followers = await _followService.GetFollowersAsync(id);

                if (followers == null)
                    return NotFound($"User with ID {id} not found.");

                return Ok(followers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving followers for user {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// GET: /api/v1/users/{id}/following
        /// Get a list of users that the specified user follows.
        /// </summary>
        [HttpGet("{id:guid}/following")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult<IEnumerable<FollowUserResponse>>> GetFollowing(Guid id)
        {
            try
            {
                var following = await _followService.GetFollowingAsync(id);

                if (following == null)
                    return NotFound($"User with ID {id} not found.");

                return Ok(following);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving following for user {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// GET: /api/v1/users/{id}/friends
        /// Get a list of mutual follows (friends) for the specified user.
        /// </summary>
        [HttpGet("{id:guid}/friends")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult<IEnumerable<FollowUserResponse>>> GetFriends(Guid id)
        {
            try
            {
                var friends = await _followService.GetFriendsAsync(id);

                if (friends == null)
                    return NotFound($"User with ID {id} not found.");

                return Ok(friends);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving friends for user {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// GET: /api/v1/users/{id}/relationship
        /// Get the relationship status between the logged-in user and another user.
        /// Used by the frontend to decide which button to show on a profile page.
        /// </summary>
        [HttpGet("{id:guid}/relationship")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult<RelationshipStatusResponse>> GetRelationship(Guid id)
        {
            var currentUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId)
                ? parsedId : (Guid?)null;

            if (currentUserId == null)
                return Unauthorized();

            if (currentUserId == id)
                return BadRequest("Cannot check relationship status with yourself.");

            try
            {
                var status = await _followService.GetRelationshipStatusAsync(currentUserId.Value, id);

                if (status == null)
                    return NotFound($"User with ID {id} not found.");

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving relationship status with user {TargetUserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}