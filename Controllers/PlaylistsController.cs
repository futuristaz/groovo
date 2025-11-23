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
    public class PlaylistsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PlaylistsController> _logger;

        public PlaylistsController(ApplicationDbContext context, ILogger<PlaylistsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>GET: /api/v1/playlists</summary>
        /// <returns>List of playlists</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PlaylistSummaryResponse>>> GetAll()
        {
            try
            {
                var playlists = await _context.Playlists
                    .Include(p => p.PlaylistSongs)
                    .Where(p => p.IsPublic)
                    .OrderBy(p => p.CreatedAt)
                    .ToListAsync();

                var playlistSummaries = playlists.Select(p => new PlaylistSummaryResponse(
                    p.Id,
                    p.Name,
                    p.Description ?? "",
                    p.Picture ?? "",
                    p.IsPublic,
                    p.IsAlbum,
                    p.TotalTime,
                    p.PlaylistSongs?.Count ?? 0
                )).ToList();

                return Ok(playlistSummaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving playlists");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>GET: /api/v1/playlists/{id}</summary>
        /// <returns>Specific playlist with songs and owners or 404 if not found</returns>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<PlaylistResponse>> GetById(Guid id)
        {
            try
            {
                var playlist = await _context.Playlists
                    .Include(p => p.PlaylistSongs)
                    .ThenInclude(ps => ps.Song)
                    .ThenInclude(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                    .Include(p => p.PlaylistOwners)
                    .ThenInclude(po => po.User)
                    .Where(p => p.Id == id)
                    .FirstOrDefaultAsync();

                if (playlist == null)
                    return NotFound($"Playlist with ID {id} not found.");

                var playlistResponse = new PlaylistResponse(
                    playlist.Id,
                    playlist.Name,
                    playlist.Description ?? "",
                    playlist.Picture ?? "",
                    playlist.IsActive,
                    playlist.IsPublic,
                    playlist.IsAlbum,
                    playlist.CreatedAt,
                    playlist.UpdatedAt,
                    playlist.TotalTime,
                    playlist.PlaylistSongs?.Count ?? 0,
                    playlist.PlaylistSongs?.Select(ps => new SongSummaryResponse(
                        ps.Song,
                        ps.Song.SongAuthors?.Where(sa => sa.User.Role == UserRole.Author)
                            .Select(sa => sa.User.Name).ToList() ?? new List<string>()
                    )).ToList() ?? new List<SongSummaryResponse>(),
                    playlist.PlaylistOwners?.Select(po => new UserSummaryResponse(
                        po.User.Id,
                        po.User.Name,
                        po.User.ImageUrl ?? "",
                        po.User.Role
                    )).ToList() ?? new List<UserSummaryResponse>()
                );

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

            if (!User.IsInRole("Admin"))
            {
                if (User.IsInRole("Author") && !request.IsAlbum)
                {
                    return Forbid("Authors can only create albums.");
                }
                else if (User.IsInRole("User") && request.IsAlbum)
                {
                    return Forbid("Regular users cannot create albums.");
                }
            }

            var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)??"");

            try
            {
                // Validate owner IDs BEFORE creating the playlist
                List<Guid> validatedOwnerIds = new List<Guid>();
                if (request.OwnerIds != null && request.OwnerIds.Any())
                {
                    var existingUserIds = await _context.Users
                        .Where(u => request.OwnerIds.Contains(u.Id))
                        .Select(u => u.Id)
                        .ToListAsync();

                    var invalidUserIds = request.OwnerIds.Except(existingUserIds).ToList();
                    if (invalidUserIds.Any())
                    {
                        return BadRequest($"The following user IDs do not exist: {string.Join(", ", invalidUserIds)}");
                    }

                    validatedOwnerIds = existingUserIds;
                }

                var newPlaylist = new Playlist
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Description = request.Description ?? "",
                    Picture = request.Picture ?? "",
                    IsPublic = request.IsPublic,
                    IsAlbum = request.IsAlbum,
                    IsActive = true,
                    TotalDuration = 0
                };

                _context.Playlists.Add(newPlaylist);

                // Add validated playlist owners in the same transaction
                if (validatedOwnerIds.Any())
                {
                    var playlistOwners = validatedOwnerIds.Select(ownerId => new PlaylistOwner
                    {
                        PlaylistId = newPlaylist.Id,
                        UserId = ownerId
                    }).ToList();

                    _context.PlaylistOwners.AddRange(playlistOwners);
                }

                // Save everything in a single transaction
                await _context.SaveChangesAsync();

                // Fetch the created playlist with owners for response
                var createdPlaylist = await _context.Playlists
                    .Include(p => p.PlaylistOwners)
                    .ThenInclude(po => po.User)
                    .FirstOrDefaultAsync(p => p.Id == newPlaylist.Id);

                if (createdPlaylist == null)
                {
                    _logger.LogError("Failed to retrieve the created playlist {PlaylistId}", newPlaylist.Id);
                    return StatusCode(500, "Internal server error");
                }

                var playlistResponse = new PlaylistResponse(
                    createdPlaylist.Id,
                    createdPlaylist.Name,
                    createdPlaylist.Description ?? "",
                    createdPlaylist.Picture ?? "",
                    createdPlaylist.IsActive,
                    createdPlaylist.IsPublic,
                    createdPlaylist.IsAlbum,
                    createdPlaylist.CreatedAt,
                    createdPlaylist.UpdatedAt,
                    createdPlaylist.TotalTime,
                    0, // No songs yet
                    new List<SongSummaryResponse>(),
                    createdPlaylist.PlaylistOwners?.Select(po => new UserSummaryResponse(
                        po.User.Id,
                        po.User.Name,
                        po.User.ImageUrl ?? "",
                        po.User.Role
                    )).ToList() ?? new List<UserSummaryResponse>()
                );

                _logger.LogInformation("Created new playlist {PlaylistId}: {PlaylistName}", newPlaylist.Id, newPlaylist.Name);

                return CreatedAtAction(nameof(GetById), new { id = newPlaylist.Id }, playlistResponse);
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
                var existing = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == id && (User.IsInRole("Admin") ||
                    p.PlaylistOwners.Any(po => po.UserId == Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)??""))));
                if (existing == null)
                    return NotFound($"Playlist with ID {id} not found.");

                if (request.Name != null)
                    existing.Name = request.Name;
                if (request.Description != null)
                    existing.Description = request.Description;
                if (request.Picture != null)
                    existing.Picture = request.Picture;
                if (request.IsPublic.HasValue)
                    existing.IsPublic = request.IsPublic.Value;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated playlist {PlaylistId}: {PlaylistName}", id, existing.Name);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating playlist {PlaylistId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>DELETE: /api/v1/playlists/{id}</summary>
        /// <returns>204 if successful, 404 if not found</returns>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "User,Author,Admin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            try
            {
                var playlist = await _context.Playlists
                    .Include(p => p.PlaylistSongs)
                    .Include(p => p.PlaylistOwners)
                    .FirstOrDefaultAsync(p => p.Id == id && (User.IsInRole("Admin") ||
                        p.PlaylistOwners.Any(po => po.UserId == Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)??""))));

                if (playlist == null)
                    return NotFound($"Playlist with ID {id} not found.");

                // If it is album and has songs, prevent deletion
                if (playlist.IsAlbum && playlist.PlaylistSongs.Any())
                {
                    return BadRequest("Cannot delete an album that contains songs.");
                }

                // Remove all related PlaylistSong entries
                if (playlist.PlaylistSongs.Any())
                {
                    _context.PlaylistSongs.RemoveRange(playlist.PlaylistSongs);
                }

                // Remove all related PlaylistOwner entries
                if (playlist.PlaylistOwners.Any())
                {
                    _context.PlaylistOwners.RemoveRange(playlist.PlaylistOwners);
                }

                // Remove the playlist itself
                _context.Playlists.Remove(playlist);
                
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted playlist {PlaylistId}: {PlaylistName} and all related entries", id, playlist.Name);

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

        /// <summary>GET: /api/v1/playlists/{id}/songs</summary>
        /// <returns>List of songs in the playlist</returns>
        [HttpGet("{id:guid}/songs")]
        public async Task<ActionResult<IEnumerable<SongSummaryResponse>>> GetSongsInPlaylist(Guid id)
        {
            try
            {
                var playlist = await _context.Playlists
                    .Include(p => p.PlaylistSongs)
                    .ThenInclude(ps => ps.Song)
                    .ThenInclude(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                    .Where(p => p.Id == id)
                    .FirstOrDefaultAsync();

                if (playlist == null)
                    return NotFound($"Playlist with ID {id} not found.");

                var songs = playlist.PlaylistSongs
                    .Where(ps => ps.Song.IsActive)
                    .OrderBy(ps => ps.Order)
                    .Select(ps => new SongSummaryResponse(
                        ps.Song,
                        ps.Song.SongAuthors?.Where(sa => sa.User.Role == UserRole.Author)
                            .Select(sa => sa.User.Name).ToList() ?? new List<string>()
                    )).ToList();

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
                var playlist = await _context.Playlists
                    .Where(p => p.Id == playlistId && !p.IsAlbum)
                    .Include(p => p.PlaylistSongs)
                    .FirstOrDefaultAsync(p => User.IsInRole("Admin") ||
                        p.PlaylistOwners.Any(po => po.UserId == Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)??"")));

                if (playlist == null)
                    return NotFound($"Playlist with ID {playlistId} not found.");

                var song = await _context.Songs
                    .FirstOrDefaultAsync(s => s.Id == songId && s.IsActive);

                if (song == null)
                    return NotFound($"Song with ID {songId} not found.");

                // Check if song is already in playlist
                var existingEntry = playlist.PlaylistSongs.FirstOrDefault(ps => ps.SongId == songId);
                if (existingEntry != null)
                    return Conflict("Song already in playlist.");

                // Get the next position
                var nextOrder = playlist.PlaylistSongs.Any() 
                    ? playlist.PlaylistSongs.Max(ps => ps.Order) + 1 
                    : 0;

                var playlistSong = new PlaylistSong
                {
                    PlaylistId = playlistId,
                    SongId = songId,
                    Order = nextOrder
                };

                _context.PlaylistSongs.Add(playlistSong);

                // Update playlist total time using Duration operators
                playlist.TotalDuration += song.Duration;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Added song {SongId} to playlist {PlaylistId}", songId, playlistId);

                return Ok($"Added song '{song.Name}' to playlist '{playlist.Name}'.");
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
                var playlist = await _context.Playlists
                    .Where(p => p.Id == playlistId && !p.IsAlbum)
                    .Include(p => p.PlaylistSongs)
                    .FirstOrDefaultAsync(p => User.IsInRole("Admin") ||
                        p.PlaylistOwners.Any(po => po.UserId == Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)??"")));

                if (playlist == null)
                    return NotFound($"Playlist with ID {playlistId} not found.");

                var playlistSong = playlist.PlaylistSongs.FirstOrDefault(ps => ps.SongId == songId);
                if (playlistSong == null)
                    return NotFound($"Song with ID {songId} not found in this playlist.");

                var song = await _context.Songs.FirstOrDefaultAsync(s => s.Id == songId);

                _context.PlaylistSongs.Remove(playlistSong);

                // Update playlist total time
                if (song != null)
                {
                    playlist.TotalDuration -= song.Duration;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Removed song {SongId} from playlist {PlaylistId}", songId, playlistId);

                return Ok($"Removed song from playlist '{playlist.Name}'.");
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
                var playlists = await _context.Playlists
                    .Include(p => p.PlaylistSongs)
                    .Where(p => p.IsPublic && (
                        p.Name.Contains(query) ||
                        (p.Description != null && p.Description.Contains(query))
                    ))
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                var playlistSummaries = playlists.Select(p => new PlaylistSummaryResponse(
                    p.Id,
                    p.Name,
                    p.Description ?? "",
                    p.Picture ?? "",
                    p.IsPublic,
                    p.IsAlbum,
                    p.TotalTime,
                    p.PlaylistSongs?.Count ?? 0
                )).ToList();

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
