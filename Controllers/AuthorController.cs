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
        private readonly ISongFileService _songFileService;

        public AuthorController(
            ApplicationDbContext context, 
            ILogger<AuthorController> logger,
            ISongFileService songFileService)
        {
            _context = context;
            _logger = logger;
            _songFileService = songFileService;
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

        /// <summary>
        /// POST: /api/v1/author/song
        /// Song should be created by authors and their own ID must be in AuthorIds.
        /// </summary>
        /// <returns>201 with the created song</returns>
        [HttpPost("song")]
        [Authorize(Policy = "AuthorPolicy")]
        public async Task<ActionResult<SongResponse>> CreateSong([FromBody] CreateSongRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.AuthorIds == null || !request.AuthorIds.Contains(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)??"")))
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
        /// DELETE: /api/v1/author/song/{id}
        /// Only authors who own the song and admins can delete it.
        /// </summary>
        /// <returns>204 if successful, 404 if not found</returns>
        [HttpDelete("song/{id:guid}")]
        [Authorize(Policy = "AuthorPolicy,AdminPolicy")]
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

    }
}