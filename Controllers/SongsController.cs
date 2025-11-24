using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Groovo.Models;
using Groovo.Data.Contexts;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Groovo.DTOs;

namespace Groovo.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class SongsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SongsController> _logger;
        private readonly ISongFileService _songFileService;

        public SongsController(ApplicationDbContext context, ILogger<SongsController> logger, ISongFileService songFileService)
        {
            _context = context;
            _logger = logger;
            _songFileService = songFileService;
        }

        /// <summary>GET: /api/v1/songs/{id}</summary>
        /// <returns>Specific song with authors or 404 if not found</returns>
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "User")]
        public async Task<ActionResult<SongResponse>> GetById(Guid id)
        {
            try
            {
                var song = await _context.Songs
                    .Include(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                    .Where(s => s.IsActive && s.Id == id)
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

            if (!User.IsInRole("Admin") && (request.AuthorIds == null || !request.AuthorIds.Contains(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)??""))))
            {
                return Forbid("Authors can only create songs for themselves.");
            }

            try
            {
                // Validate both audio and image IDs exist in temp storage
                var audioExists = await _songFileService.FileExistsAsync(request.AudioId);
                if (!audioExists)
                {
                    return BadRequest($"Audio file not found for upload ID: {request.AudioId}");
                }

                var imageExists = await _songFileService.FileExistsAsync(request.ImageId);
                if (!imageExists)
                {
                    return BadRequest($"Image file not found for upload ID: {request.ImageId}");
                }

                List<Guid> validatedAuthorIds = new List<Guid>();
                if (request.AuthorIds != null && request.AuthorIds.Any())
                {
                    var existingAuthorIds = await _context.Users
                        .Where(u => request.AuthorIds.Contains(u.Id) && u.Role == UserRole.Author)
                        .Select(u => u.Id)
                        .ToListAsync();

                    var invalidAuthorIds = request.AuthorIds.Except(existingAuthorIds).ToList();
                    if (invalidAuthorIds.Any())
                    {
                        return BadRequest($"The following author IDs do not exist or are not authors: {string.Join(", ", invalidAuthorIds)}");
                    }

                    validatedAuthorIds = existingAuthorIds;
                }

                // Get audio duration before moving files
                int audioDuration;
                try
                {
                    audioDuration = await _songFileService.GetAudioDurationAsync(request.AudioId);
                    if (audioDuration <= 0)
                    {
                        _logger.LogWarning("Could not determine audio duration for upload ID: {AudioId}", request.AudioId);
                        return BadRequest("Unable to determine audio file duration. The file may be corrupted or in an unsupported format.");
                    }
                    _logger.LogInformation("Audio duration: {Duration} seconds", audioDuration);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read audio duration for upload ID: {AudioId}", request.AudioId);
                    return BadRequest($"Failed to read audio file duration: {ex.Message}");
                }

                // Move files from temp to final storage
                string audioFilePath;
                string imageFilePath;

                try
                {
                    audioFilePath = await _songFileService.MoveUploadedFileAsync(request.AudioId, "audio");
                    _logger.LogInformation("Moved audio file to: {AudioPath}", audioFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move audio file for upload ID: {AudioId}", request.AudioId);
                    return BadRequest($"Failed to process audio file: {ex.Message}");
                }

                try
                {
                    imageFilePath = await _songFileService.MoveUploadedFileAsync(request.ImageId, "images");
                    _logger.LogInformation("Moved image file to: {ImagePath}", imageFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move image file for upload ID: {ImageId}", request.ImageId);
                    return BadRequest($"Failed to process image file: {ex.Message}");
                }

                var newSong = new Song
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Description = request.Description,
                    ReleaseDate = request.ReleaseDate,
                    Picture = imageFilePath,
                    Album = request.Album,
                    Genre = request.Genre,
                    Tags = string.Join(",", request.Tags),
                    AudioUrl = audioFilePath,
                    Duration = new Models.Duration(audioDuration),
                    IsActive = true
                };

                _context.Songs.Add(newSong);

                if (validatedAuthorIds.Any())
                {
                    var songAuthors = validatedAuthorIds.Select(authorId => new SongAuthor
                    {
                        SongId = newSong.Id,
                        UserId = authorId
                    }).ToList();

                    _context.SongAuthors.AddRange(songAuthors);
                }

                // Add PlaylistSong entry for the album
                var playlistSong = new PlaylistSong
                {
                    PlaylistId = request.Album,
                    SongId = newSong.Id,
                    Order = 0, // Will be updated based on existing songs in playlist
                    AddedAt = DateTime.UtcNow
                };

                // Get the current max order in the album and increment
                var maxOrder = await _context.PlaylistSongs
                    .Where(ps => ps.PlaylistId == request.Album)
                    .MaxAsync(ps => (int?)ps.Order) ?? -1;
                playlistSong.Order = maxOrder + 1;

                _context.PlaylistSongs.Add(playlistSong);

                await _context.SaveChangesAsync();

                var createdSong = await _context.Songs
                    .Include(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                    .FirstOrDefaultAsync(s => s.Id == newSong.Id);

                if (createdSong == null)
                {
                    _logger.LogError("Failed to retrieve the created song {SongId}", newSong.Id);
                    return StatusCode(500, "Internal server error");
                }

                var songResponse = new SongResponse(
                    createdSong,
                    createdSong.SongAuthors.Where(sa => sa.User.Role == UserRole.Author).Select(sa => new AuthorResponse(
                        sa.User.Id,
                        sa.User.Name,
                        sa.User.Bio,
                        sa.User.ImageUrl
                    )).ToList()
                );

                _logger.LogInformation("Created new song {SongId}: {SongName}", newSong.Id, newSong.Name);

                return CreatedAtAction(nameof(GetById), new { id = newSong.Id }, songResponse);
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
                var existing = await _context.Songs.FirstOrDefaultAsync(s => s.Id == id && (User.IsInRole("Admin") ||
                        s.SongAuthors.Any(sa => sa.UserId.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier))));
                if (existing == null)
                    return NotFound($"Song with ID {id} not found.");

                if (request.Album.HasValue) {
                    // Check if album exists, it is album and author owns it
                    var album = await _context.Playlists
                        .Where(p => p.Id == request.Album.Value && p.IsAlbum &&
                            (User.IsInRole("Admin") ||
                                p.PlaylistOwners.Any(po => po.UserId.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier))))
                        .FirstOrDefaultAsync();
                    if (album == null)
                        return BadRequest("Invalid album ID or you do not have permission to assign this album.");
                    
                    // If album is changing, update PlaylistSong entries
                    if (existing.Album != request.Album.Value)
                    {
                        // Remove from old album
                        var oldPlaylistSong = await _context.PlaylistSongs
                            .FirstOrDefaultAsync(ps => ps.PlaylistId == existing.Album && ps.SongId == id);
                        if (oldPlaylistSong != null)
                        {
                            _context.PlaylistSongs.Remove(oldPlaylistSong);
                        }

                        // Add to new album
                        var maxOrder = await _context.PlaylistSongs
                            .Where(ps => ps.PlaylistId == request.Album.Value)
                            .MaxAsync(ps => (int?)ps.Order) ?? -1;

                        var newPlaylistSong = new PlaylistSong
                        {
                            PlaylistId = request.Album.Value,
                            SongId = id,
                            Order = maxOrder + 1,
                            AddedAt = DateTime.UtcNow
                        };

                        _context.PlaylistSongs.Add(newPlaylistSong);
                    }
                    
                    existing.Album = request.Album.Value;
                }

                if (request.Name != null)
                    existing.Name = request.Name;
                    
                if (request.Description != null)
                    existing.Description = request.Description;
                    
                if (request.Genre != null)
                    existing.Genre = request.Genre;
                    
                if (request.Tags != null)
                    existing.Tags = string.Join(",", request.Tags);
                    
                if (request.ReleaseDate.HasValue)
                    existing.ReleaseDate = request.ReleaseDate.Value;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated song {SongId}: {SongName}", id, existing.Name);

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
                var song = await _context.Songs
                    .Where(s => s.Id == id && (User.IsInRole("Admin") ||
                        s.SongAuthors.Any(sa => sa.UserId.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier))))
                    .Include(s => s.SongAuthors)
                    .Include(s => s.PlaylistSongs)
                    .FirstOrDefaultAsync();
                
                if (song == null)
                    return NotFound($"Song with ID {id} not found.");

                if (song.SongAuthors.Any())
                {
                    _context.SongAuthors.RemoveRange(song.SongAuthors);
                }

                if (song.PlaylistSongs.Any())
                {
                    _context.PlaylistSongs.RemoveRange(song.PlaylistSongs);
                }

                _context.Songs.Remove(song);
                
                await _context.SaveChangesAsync();

                // Delete associated files from storage
                if (!string.IsNullOrWhiteSpace(song.AudioUrl))
                {
                    var audioDeleted = await _songFileService.DeleteFileAsync(song.AudioUrl);
                    if (audioDeleted)
                    {
                        _logger.LogInformation("Deleted audio file: {AudioUrl}", song.AudioUrl);
                    }
                }

                if (!string.IsNullOrWhiteSpace(song.Picture))
                {
                    var imageDeleted = await _songFileService.DeleteFileAsync(song.Picture);
                    if (imageDeleted)
                    {
                        _logger.LogInformation("Deleted image file: {Picture}", song.Picture);
                    }
                }

                _logger.LogInformation("Deleted song {SongId}: {SongName} and all related entries", id, song.Name);

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
                var songs = await _context.Songs
                    .Include(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                    .Where(s => s.IsActive && (
                        s.Name.Contains(query) ||
                        s.Genre.Contains(query) ||
                        s.SongAuthors.Any(sa => sa.User.Name.Contains(query))
                    ))
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                var songSummaries = songs.Select(s => new SongSummaryResponse(
                    s,
                    s.SongAuthors.Where(sa => sa.User.Role == UserRole.Author)
                        .Select(sa => sa.User.Name)
                        .ToList()
                )).ToList();

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
