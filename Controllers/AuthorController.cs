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
    }
}