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
    public class PlaylistsController : ControllerBase
    {
        private readonly IPlaylistManagementService _playlistService;
        private readonly ILogger<PlaylistsController> _logger;

        public PlaylistsController(IPlaylistManagementService playlistService, ILogger<PlaylistsController> logger)
        {
            _playlistService = playlistService;
            _logger = logger;
        }

        /// <summary>
        /// GET: /api/v1/playlists
        /// Only public playlists or those accessible by admins are returned
        /// </summary>
        /// <returns>List of playlists</returns>
        [HttpGet]
        [Authorize(Roles = "User,Admin")]
        public async Task<ActionResult<IEnumerable<PlaylistSummaryResponse>>> GetAll()
        {
            try
            {
                var isAdmin = User.IsInRole("Admin");
                var playlistSummaries = await _playlistService.GetAllPlaylistsAsync(isAdmin);
                return Ok(playlistSummaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving playlists");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// GET: /api/v1/playlists/{id}
        /// Only owners or admins can view non-public playlists
        /// </summary>
        /// <returns>Specific playlist with owners or 404 if not found</returns>
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult<PlaylistResponse>> GetById(Guid id)
        {
            try
            {
                var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : (Guid?)null;
                var isAdmin = User.IsInRole("Admin");

                var playlistResponse = await _playlistService.GetPlaylistByIdAsync(id, userId, isAdmin);

                if (playlistResponse == null)
                    return NotFound($"Playlist with ID {id} not found.");

                return Ok(playlistResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving playlist {PlaylistId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// POST: /api/v1/playlists
        /// If user is author, it can create only albums
        /// If user is regular user, it can create only non-album playlists
        /// If user is admin, it can create any playlist
        /// </summary>
        /// <returns>201 with the created playlist</returns>
        [HttpPost]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult<PlaylistResponse>> Create([FromBody] CreatePlaylistRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)??"");
            var userRole = User.IsInRole("Admin") ? "Admin" : User.IsInRole("Author") ? "Author" : "User";

            try
            {
                var (playlistResponse, errorMessage) = await _playlistService.CreatePlaylistAsync(request, currentUserId, userRole);

                if (playlistResponse == null)
                {
                    if (errorMessage?.Contains("can only create") == true || errorMessage?.Contains("cannot create") == true)
                        return Forbid(errorMessage);
                    return BadRequest(errorMessage);
                }

                return CreatedAtAction(nameof(GetById), new { id = playlistResponse.Id }, playlistResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating playlist {PlaylistName}", request.Name);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// PUT: /api/v1/playlists/{id}
        /// Only owners or admins can update the playlist
        /// </summary>
        /// <returns>204 if successful, 404 if not found</returns>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult> Update(Guid id, [FromBody] UpdatePlaylistRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : (Guid?)null;
                var isAdmin = User.IsInRole("Admin");

                var (success, errorMessage) = await _playlistService.UpdatePlaylistAsync(id, request, userId, isAdmin);

                if (!success)
                {
                    if (errorMessage != null)
                        return BadRequest(errorMessage);
                    return NotFound($"Playlist with ID {id} not found.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating playlist {PlaylistId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// DELETE: /api/v1/playlists/{id}
        /// Only owners or admins can delete the playlist
        /// </summary>
        /// <returns>204 if successful, 404 if not found</returns>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            try
            {
                var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : (Guid?)null;
                var isAdmin = User.IsInRole("Admin");

                var (success, errorMessage) = await _playlistService.DeletePlaylistAsync(id, userId, isAdmin);

                if (!success)
                {
                    if (errorMessage != null)
                        return BadRequest(errorMessage);
                    return NotFound($"Playlist with ID {id} not found.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting playlist {PlaylistId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        //=============================================
        // Playlist-Song management endpoints
        //=============================================

        /// <summary>
        /// GET: /api/v1/playlists/{id}/songs
        /// Only owners or admins can view songs in non-public playlists
        /// </summary>
        /// <returns>List of songs in the playlist</returns>
        [HttpGet("{id:guid}/songs")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult<IEnumerable<SongSummaryResponse>>> GetSongsInPlaylist(Guid id)
        {
            try
            {
                var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : (Guid?)null;
                var isAdmin = User.IsInRole("Admin");

                var songs = await _playlistService.GetSongsInPlaylistAsync(id, userId, isAdmin);

                if (songs == null)
                    return NotFound($"Playlist with ID {id} not found.");

                return Ok(songs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving songs for playlist {PlaylistId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// POST: /api/v1/playlists/{playlistId}/songs/{songId}
        /// Only owners or admins can add songs to the playlist (no albums)
        /// </summary>
        /// <returns>201 if successful, 404 if playlist or song not found, 409 if song already in playlist</returns>
        [HttpPost("{playlistId:guid}/songs/{songId:guid}")]
        [Authorize(Roles = "User,Admin")]
        public async Task<ActionResult> AddSongToPlaylist(Guid playlistId, Guid songId)
        {
            try
            {
                var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : (Guid?)null;
                var isAdmin = User.IsInRole("Admin");

                var (success, errorMessage) = await _playlistService.AddSongToPlaylistAsync(playlistId, songId, userId, isAdmin);

                if (!success)
                {
                    if (errorMessage?.Contains("already in playlist") == true)
                        return Conflict(errorMessage);
                    if (errorMessage?.Contains("not found") == true)
                        return NotFound(errorMessage);
                    return BadRequest(errorMessage);
                }

                return Ok(errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding song {SongId} to playlist {PlaylistId}", songId, playlistId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// DELETE: /api/v1/playlists/{playlistId}/songs/{songId}
        /// Only owners or admins can remove songs from the playlist (no albums)
        /// </summary>
        /// <returns>204 if successful, 404 if playlist or song not found</returns>
        [HttpDelete("{playlistId:guid}/songs/{songId:guid}")]
        [Authorize(Roles = "User,Admin")]
        public async Task<ActionResult> RemoveSongFromPlaylist(Guid playlistId, Guid songId)
        {
            try
            {
                var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : (Guid?)null;
                var isAdmin = User.IsInRole("Admin");

                var (success, errorMessage) = await _playlistService.RemoveSongFromPlaylistAsync(playlistId, songId, userId, isAdmin);

                if (!success)
                {
                    if (errorMessage?.Contains("not found") == true)
                        return NotFound(errorMessage);
                    return BadRequest(errorMessage);
                }

                return Ok(errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing song {SongId} from playlist {PlaylistId}", songId, playlistId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>GET: /api/v1/playlists/search</summary>
        /// <returns>List of playlists matching the search criteria</returns>
        [HttpGet("search")]
        [Authorize(Roles = "User")]
        public async Task<ActionResult<IEnumerable<PlaylistSummaryResponse>>> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query parameter is required");
            }

            try
            {
                var playlistSummaries = await _playlistService.SearchPlaylistsAsync(query);
                return Ok(playlistSummaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching playlists with query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
