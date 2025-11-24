using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Groovo.Models;
using Groovo.Data.Contexts;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
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

        public SongsController(ApplicationDbContext context, ILogger<SongsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>GET: /api/v1/songs</summary>
        /// <returns>List of songs</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SongSummaryResponse>>> GetAll()
        {
            try
            {
                var songs = await _context.Songs
                    .Include(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.ReleaseDate)
                    .ToListAsync();

                var songSummaryResponses = songs.Select(s => new SongSummaryResponse(
                    s,
                    s.SongAuthors.Where(sa => sa.User.Role == UserRole.Author)
                        .Select(sa => sa.User.Name)
                        .ToList()
                )).ToList();

                return Ok(songSummaryResponses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving songs");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>GET: /api/v1/songs/{id}</summary>
        /// <returns>Specific song with authors or 404 if not found</returns>
        [HttpGet("{id:guid}")]
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

        /// <summary>POST: /api/v1/songs</summary>
        /// <returns>201 with the created song</returns>
        [HttpPost]
        public async Task<ActionResult<SongResponse>> Create([FromBody] CreateSongRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Validate author IDs BEFORE creating the song
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

                var newSong = new Song
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Description = request.Description,
                    ReleaseDate = request.ReleaseDate,
                    Picture = request.Picture,
                    Album = request.Album,
                    Genre = request.Genre,
                    Tags = string.Join(",", request.Tags), // Convert list to comma-separated string
                    AudioUrl = request.AudioUrl,
                    Duration = new DTOs.Duration(request.Length), // Use Duration struct
                    IsActive = true
                };

                _context.Songs.Add(newSong);

                // Add validated song-author relationships in the same transaction
                if (validatedAuthorIds.Any())
                {
                    var songAuthors = validatedAuthorIds.Select(authorId => new SongAuthor
                    {
                        SongId = newSong.Id,
                        UserId = authorId
                    }).ToList();

                    _context.SongAuthors.AddRange(songAuthors);
                }

                // Save everything in a single transaction
                await _context.SaveChangesAsync();

                // Fetch the created song with authors for response
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

        /// <summary>PUT: /api/v1/songs/{id}</summary>
        /// <returns>204 if successful, 404 if not found</returns>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult> Update(Guid id, [FromBody] UpdateSongRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var existing = await _context.Songs.FirstOrDefaultAsync(s => s.Id == id);
                if (existing == null)
                    return NotFound($"Song with ID {id} not found.");

                existing.Name = request.Name;
                existing.Description = request.Description;
                existing.Picture = request.Picture;
                existing.Album = request.Album;
                existing.Genre = request.Genre;
                existing.Tags = string.Join(",", request.Tags);
                existing.AudioUrl = request.AudioUrl;
                existing.Duration = new DTOs.Duration(request.Length);

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

        /// <summary>DELETE: /api/v1/songs/{id}</summary>
        /// <returns>204 if successful, 404 if not found</returns>
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> Delete(Guid id)
        {
            try
            {
                var song = await _context.Songs
                    .Include(s => s.SongAuthors)
                    .Include(s => s.PlaylistSongs)
                    .FirstOrDefaultAsync(s => s.Id == id);
                
                if (song == null)
                    return NotFound($"Song with ID {id} not found.");

                // Remove all related SongAuthor entries
                if (song.SongAuthors.Any())
                {
                    _context.SongAuthors.RemoveRange(song.SongAuthors);
                }

                // Remove all related PlaylistSong entries
                if (song.PlaylistSongs.Any())
                {
                    _context.PlaylistSongs.RemoveRange(song.PlaylistSongs);
                }

                // Remove the song itself
                _context.Songs.Remove(song);
                
                await _context.SaveChangesAsync();

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
