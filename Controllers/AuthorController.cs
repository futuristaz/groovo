using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Groovo.Models;
using Groovo.Data.Contexts;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Groovo.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class AuthorController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthorController> _logger;

        public AuthorController(ApplicationDbContext context, ILogger<AuthorController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// GET: /api/v1/author/{id}
        /// </summary>
        /// <returns>Author details or 404 if not found</returns>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "UserPolicy,AuthorPolicy,AdminPolicy")]
        public async Task<ActionResult<AuthorResponse>> GetAuthorById(Guid id)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == id && u.Role == UserRole.Author)
                    .FirstOrDefaultAsync();

                if (user == null)
                    return NotFound($"Author with ID {id} not found.");

                var authorResponse = new AuthorResponse(
                    user.Id,
                    user.Name,
                    user.Bio,
                    user.ImageUrl
                );

                return Ok(authorResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving author {AuthorId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// GET: /api/v1/author/{id}/songs
        /// If id is the current user's ID or it is admin, returns all their authored songs.
        /// Otherwise, returns only active songs.
        /// </summary>
        /// <returns>List of songs authored by the author</returns>
        [HttpGet("{id:guid}/songs")]
        [Authorize(Policy = "UserPolicy,AuthorPolicy,AdminPolicy")]
        public async Task<ActionResult<IEnumerable<SongSummaryResponse>>> GetAuthorSongs(Guid id)
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
                    .Where(sa => sa.Song.IsActive || sa.User.Id == id || User.IsInRole("Admin"))
                    .Select(sa => sa.Song)
                    .OrderByDescending(s => s.ReleaseDate)
                    .Select(s => new SongSummaryResponse(
                        s,
                        s.SongAuthors.Where(sa => sa.User.Role == UserRole.Author)
                            .Select(sa => sa.User.Name).ToList()
                    )).ToList();

                return Ok(songs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving songs for author {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// GET: /api/v1/author/{id}/playlists
        /// If id is the current user's ID or it is admin, returns all their playlists (albums).
        /// If current user is author and it is not their ID, return error.
        /// </summary>
        /// <returns>List of playlists (aka albums) owned by the author</returns>
        [HttpGet("{id:guid}/playlists")]
        [Authorize(Policy = "AuthorPolicy,AdminPolicy")]
        public async Task<ActionResult<IEnumerable<PlaylistSummaryResponse>>> GetAuthorPlaylists(Guid id)
        {
            if (User.IsInRole("Author") && User.FindFirstValue(ClaimTypes.NameIdentifier) != id.ToString())
            {
                return Forbid("Authors can only access their own playlists.");
            }

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

        /// <summary>
        /// GET: /api/v1/author/song/{id}
        /// Only authors, if they owns song, and admins can access this endpoint.
        /// Oriented for author view
        /// </summary>
        /// <returns>Specific song with authors or 404 if not found</returns>
        [HttpGet("song/{id:guid}")]
        [Authorize(Policy = "AuthorPolicy,AdminPolicy")]
        public async Task<ActionResult<SongResponse>> GetById(Guid id)
        {
            try
            {
                var song = await _context.Songs
                    .Include(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                    .Where(s => s.Id == id && (User.IsInRole("Admin") ||
                        s.SongAuthors.Any(sa => sa.UserId.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier))))
                    .FirstOrDefaultAsync();

                if (song == null)
                    return NotFound($"Song with ID {id} not found.");

                var songResponse = new SongResponse(
                    song,
                    song.SongAuthors.Where(sa => sa.User.Role == UserRole.Author).Select(sa => new AuthorResponse(
                        sa.User.Id,
                        sa.User.Name,
                        sa.User.Bio,
                        sa.User.ImageUrl
                    )).ToList()
                );

                return Ok(songResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving song {SongId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

    }
}