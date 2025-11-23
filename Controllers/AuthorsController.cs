using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Groovo.Models;
using Groovo.Data.Contexts;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Services;
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
        /// GET: /api/v1/authors/songs/{id}
        /// Only authors, if they owns song, and admins can access this endpoint.
        /// Oriented for author view
        /// </summary>
        /// <returns>Specific song with authors or 404 if not found</returns>
        [HttpGet("songs/{id:guid}")]
        [Authorize(Roles = "Author,Admin")]
        public async Task<ActionResult<SongResponse>> GetSongById(Guid id)
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