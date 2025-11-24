using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Groovo.Models;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Services;
using System.Security.Claims;

namespace Groovo.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        /// <summary>GET: /api/v1/users</summary>
        /// <returns>List of users</returns>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<UserSummaryResponse>>> GetAll([FromQuery] UserRole? role = null)
        {
            try
            {
                var userSummaries = await _userService.GetAllUsersAsync(role);
                return Ok(userSummaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>GET: /api/v1/users/authors</summary>
        /// <returns>List of authors (users with Role = Author)</returns>
        [HttpGet("authors")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<UserSummaryResponse>>> GetAuthors()
        {
            try
            {
                var authorSummaries = await _userService.GetAuthorsAsync();
                return Ok(authorSummaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving authors");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>GET: /api/v1/users/{id}</summary>
        /// <returns>Specific user or 404 if not found</returns>
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserResponse>> GetById(Guid id)
        {
            try
            {
                var userResponse = await _userService.GetUserByIdAsync(id);

                if (userResponse == null)
                    return NotFound($"User with ID {id} not found.");

                return Ok(userResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>PUT: /api/v1/users/{id}</summary>
        /// <returns>204 if successful, 404 if not found</returns>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var success = await _userService.UpdateUserAsync(id, request.Name, request.Bio, request.ImageUrl);
                
                if (!success)
                    return NotFound($"User with ID {id} not found.");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>DELETE: /api/v1/users/{id}</summary>
        /// <returns>204 if successful, 404 if not found</returns>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _userService.DeleteUserAsync(id);
                
                if (!success)
                    return NotFound($"User with ID {id} not found.");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// GET: /api/v1/users/authors/{id}
        /// </summary>
        /// <returns>Author details or 404 if not found</returns>
        [HttpGet("authors/{id:guid}")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult<AuthorResponse>> GetAuthorById(Guid id)
        {
            try
            {
                var authorResponse = await _userService.GetAuthorByIdAsync(id);

                if (authorResponse == null)
                    return NotFound($"Author with ID {id} not found.");

                return Ok(authorResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving author {AuthorId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// GET: /api/v1/users/authors/{id}/songs
        /// If id is the current user's ID or it is admin, returns all their authored songs.
        /// Otherwise, returns only active songs.
        /// </summary>
        /// <returns>List of songs authored by the author</returns>
        [HttpGet("authors/{id:guid}/songs")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult<IEnumerable<SongSummaryResponse>>> GetAuthorSongs(Guid id)
        {
            try
            {
                var requestingUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : (Guid?)null;
                var isAdmin = User.IsInRole("Admin");

                var songs = await _userService.GetAuthorSongsAsync(id, requestingUserId, isAdmin);

                if (songs.Count == 0)
                {
                    var author = await _userService.GetAuthorByIdAsync(id);
                    if (author == null)
                        return NotFound($"User with ID {id} not found.");
                }

                return Ok(songs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving songs for author {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// GET: /api/v1/users/{id}/playlists
        /// Only the user themselves or admins can view all playlists
        /// Otherwise only public playlists are shown
        /// Authors can only access their own playlists.
        /// </summary>
        /// <returns>List of playlists owned by the user</returns>
        [HttpGet("{id:guid}/playlists")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult<IEnumerable<PlaylistSummaryResponse>>> GetUserPlaylists(Guid id)
        {
            if (User.IsInRole("Author") && User.FindFirstValue(ClaimTypes.NameIdentifier) != id.ToString())
            {
                return Forbid("Authors can only access their own playlists.");
            }

            try
            {
                bool showFullList = User.IsInRole("Admin") || Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "") == id;

                var playlists = await _userService.GetUserPlaylistsAsync(id, showFullList);

                if (playlists.Count == 0)
                {
                    var user = await _userService.GetUserByIdAsync(id);
                    if (user == null)
                        return NotFound($"User with ID {id} not found.");
                }

                return Ok(playlists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving playlists for user {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>GET: /api/v1/users/search</summary>
        /// <returns>List of users matching the search criteria</returns>
        [HttpGet("search")]
        [Authorize(Roles = "User,Admin")]
        public async Task<ActionResult<IEnumerable<UserSummaryResponse>>> Search([FromQuery] string query, [FromQuery] UserRole? role = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query parameter is required");
            }

            try
            {
                var userSummaries = await _userService.SearchUsersAsync(query, role);
                return Ok(userSummaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}