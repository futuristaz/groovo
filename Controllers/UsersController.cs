using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Groovo.Models;
using Groovo.Data.Contexts;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;

namespace Groovo.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UsersController> _logger;

        public UsersController(ApplicationDbContext context, ILogger<UsersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>GET: /api/v1/users</summary>
        /// <returns>List of users</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserSummaryResponse>>> GetAll([FromQuery] UserRole? role = null)
        {
            try
            {
                var query = _context.Users.AsQueryable();

                if (role.HasValue)
                {
                    query = query.Where(u => u.Role == role.Value);
                }

                var users = await query
                    .OrderBy(u => u.Name)
                    .ToListAsync();

                var userSummaries = users.Select(u => new UserSummaryResponse(
                    u.Id,
                    u.Name,
                    u.ImageUrl ?? "",
                    u.Role
                )).ToList();

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
        public async Task<ActionResult<IEnumerable<UserSummaryResponse>>> GetAuthors()
        {
            try
            {
                var authors = await _context.Users
                    .Where(u => u.Role == UserRole.Author)
                    .OrderBy(u => u.Name)
                    .ToListAsync();

                var authorSummaries = authors.Select(a => new UserSummaryResponse(
                    a.Id,
                    a.Name,
                    a.ImageUrl ?? "",
                    a.Role
                )).ToList();

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
        public async Task<ActionResult<UserResponse>> GetById(Guid id)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                    return NotFound($"User with ID {id} not found.");

                var userResponse = new UserResponse(
                    user.Id,
                    user.Name,
                    user.Bio ?? "",
                    user.ImageUrl ?? "",
                    user.Role,
                    user.CreatedAt,
                    user.UpdatedAt
                );

                return Ok(userResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>POST: /api/v1/users</summary>
        /// <returns>201 with the created user</returns>
        [HttpPost]
        public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var newUser = new User
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Bio = request.Bio ?? "",
                    ImageUrl = request.ImageUrl ?? "",
                    Role = request.Role
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                var userResponse = new UserResponse(
                    newUser.Id,
                    newUser.Name,
                    newUser.Bio ?? "",
                    newUser.ImageUrl ?? "",
                    newUser.Role,
                    newUser.CreatedAt,
                    newUser.UpdatedAt
                );

                _logger.LogInformation("Created new user {UserId}: {UserName}", newUser.Id, newUser.Name);

                return CreatedAtAction(nameof(GetById), new { id = newUser.Id }, userResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user {UserName}", request.Name);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>PUT: /api/v1/users/{id}</summary>
        /// <returns>204 if successful, 404 if not found</returns>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var existing = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
                if (existing == null)
                    return NotFound($"User with ID {id} not found.");

                existing.Name = request.Name;
                existing.Bio = request.Bio ?? "";
                existing.ImageUrl = request.ImageUrl ?? "";
                // UpdatedAt is handled automatically by the context

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated user {UserId}: {UserName}", id, existing.Name);

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
        public async Task<ActionResult> Delete(Guid id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.SongAuthors)
                    .Include(u => u.PlaylistOwners)
                    .FirstOrDefaultAsync(u => u.Id == id);
                
                if (user == null)
                    return NotFound($"User with ID {id} not found.");

                // Remove all related SongAuthor entries
                if (user.SongAuthors.Any())
                {
                    _context.SongAuthors.RemoveRange(user.SongAuthors);
                }

                // Remove all related PlaylistOwner entries
                if (user.PlaylistOwners.Any())
                {
                    _context.PlaylistOwners.RemoveRange(user.PlaylistOwners);
                }

                // Remove the user itself
                _context.Users.Remove(user);
                
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted user {UserId}: {UserName} and all related entries", id, user.Name);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>GET: /api/v1/users/{id}/songs</summary>
        /// <returns>List of songs authored by the user</returns>
        [HttpGet("{id:guid}/songs")]
        public async Task<ActionResult<IEnumerable<SongSummaryResponse>>> GetUserSongs(Guid id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.SongAuthors)
                    .ThenInclude(sa => sa.Song)
                    .ThenInclude(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                    return NotFound($"User with ID {id} not found.");

                var songs = user.SongAuthors
                    .Where(sa => sa.Song.IsActive)
                    .Select(sa => sa.Song)
                    .OrderByDescending(s => s.ReleaseDate)
                    .Select(s => new SongSummaryResponse(
                        s.Id,
                        s.Name,
                        s.Genre,
                        s.ReleaseDate,
                        s.Picture,
                        s.Length,
                        s.Plays,
                        s.Likes,
                        s.SongAuthors.Where(sa => sa.User.Role == UserRole.Author)
                            .Select(sa => sa.User.Name).ToList()
                    )).ToList();

                return Ok(songs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving songs for user {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>GET: /api/v1/users/{id}/playlists</summary>
        /// <returns>List of playlists owned by the user</returns>
        [HttpGet("{id:guid}/playlists")]
        public async Task<ActionResult<IEnumerable<PlaylistSummaryResponse>>> GetUserPlaylists(Guid id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.PlaylistOwners)
                    .ThenInclude(po => po.Playlist)
                    .ThenInclude(p => p.PlaylistSongs)
                    .Include(u => u.PlaylistOwners)
                    .ThenInclude(po => po.Playlist)
                    .ThenInclude(p => p.PlaylistOwners)
                    .ThenInclude(po => po.User)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                    return NotFound($"User with ID {id} not found.");

                var playlists = user.PlaylistOwners
                    .Where(po => po.Playlist.IsActive)
                    .Select(po => po.Playlist)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new PlaylistSummaryResponse(
                        p.Id,
                        p.Name,
                        p.Description ?? "",
                        p.Picture ?? "",
                        p.IsPublic,
                        p.IsAlbum,
                        p.TotalTime,
                        p.PlaylistSongs?.Count ?? 0
                    )).ToList();

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
        public async Task<ActionResult<IEnumerable<UserSummaryResponse>>> Search([FromQuery] string query, [FromQuery] UserRole? role = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query parameter is required");
            }

            try
            {
                var queryable = _context.Users
                    .Where(u => u.Name.Contains(query) || 
                           (u.Bio != null && u.Bio.Contains(query)));

                if (role.HasValue)
                {
                    queryable = queryable.Where(u => u.Role == role.Value);
                }

                var users = await queryable
                    .OrderBy(u => u.Name)
                    .ToListAsync();

                var userSummaries = users.Select(u => new UserSummaryResponse(
                    u.Id,
                    u.Name,
                    u.ImageUrl ?? "",
                    u.Role
                )).ToList();

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