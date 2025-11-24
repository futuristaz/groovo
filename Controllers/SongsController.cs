using Microsoft.AspNetCore.Mvc;
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
    public class SongsController : ControllerBase
    {
        private readonly ISongService _songService;
        private readonly ILogger<SongsController> _logger;

        public SongsController(ISongService songService, ILogger<SongsController> logger)
        {
            _songService = songService;
            _logger = logger;
        }

        /// <summary>GET: /api/v1/songs/{id}</summary>
        /// <returns>Specific song with authors or 404 if not found</returns>
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "User")]
        public async Task<ActionResult<SongResponse>> GetById(Guid id)
        {
            try
            {
                var songResponse = await _songService.GetSongByIdAsync(id);

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

        /// <summary>
        /// POST: /api/v1/songs
        /// Song should be created by authors and their own ID must be in AuthorIds.
        /// </summary>
        /// <returns>201 with the created song</returns>
        [HttpPost]
        [Authorize(Roles = "Author,Admin")]
        public async Task<ActionResult<SongResponse>> CreateSongAuthor([FromBody] CreateSongRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var authorId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;
                var isAdmin = User.IsInRole("Admin");

                var (songResponse, errorMessage) = await _songService.CreateSongAsync(request, authorId, isAdmin);

                if (songResponse == null)
                {
                    if (errorMessage == "Authors can only create songs for themselves.")
                        return Forbid(errorMessage);
                    return BadRequest(errorMessage);
                }

                return CreatedAtAction(nameof(GetById), new { id = songResponse.Id }, songResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating song {SongName}", request.Name);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// PUT: /api/v1/songs/{id}
        /// Only authors who own the song and admins can update it.
        /// </summary>
        /// <returns>204 if successful, 404 if not found</returns>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Author,Admin")]
        public async Task<ActionResult> UpdateSong(Guid id, [FromBody] UpdateSongRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : (Guid?)null;
                var isAdmin = User.IsInRole("Admin");

                var (success, errorMessage) = await _songService.UpdateSongAsync(id, request, userId, isAdmin);

                if (!success)
                {
                    if (errorMessage != null)
                        return BadRequest(errorMessage);
                    return NotFound($"Song with ID {id} not found.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating song {SongId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// DELETE: /api/v1/songs/{id}
        /// Only authors who own the song and admins can delete it.
        /// </summary>
        /// <returns>204 if successful, 404 if not found</returns>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Author,Admin")]
        public async Task<ActionResult> DeleteSong(Guid id)
        {
            try
            {
                var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : (Guid?)null;
                var isAdmin = User.IsInRole("Admin");

                var success = await _songService.DeleteSongAsync(id, userId, isAdmin);

                if (!success)
                    return NotFound($"Song with ID {id} not found.");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting song {SongId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>GET: /api/v1/songs/search</summary>
        /// <returns>List of songs matching the search criteria</returns>
        [HttpGet("search")]
        [Authorize(Roles = "User")]
        public async Task<ActionResult<IEnumerable<SongSummaryResponse>>> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query parameter is required");
            }

            try
            {
                var songSummaries = await _songService.SearchSongsAsync(query);
                return Ok(songSummaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching songs with query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
