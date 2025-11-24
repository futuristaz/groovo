using Microsoft.AspNetCore.Mvc;
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
        private readonly IAuthorService _authorService;
        private readonly ILogger<AuthorController> _logger;

        public AuthorController(IAuthorService authorService, ILogger<AuthorController> logger)
        {
            _authorService = authorService;
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
                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var isAdmin = User.IsInRole("Admin");

                var songResponse = await _authorService.GetAuthorSongByIdAsync(id, userId, isAdmin);

                if (songResponse == null)
                    return NotFound($"Song with ID {id} not found.");

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