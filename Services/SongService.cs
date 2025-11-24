using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.DTOs;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Models;

namespace Groovo.Services
{
    public class SongService : ISongService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SongService> _logger;
        private readonly ISongFileService _songFileService;

        public SongService(ApplicationDbContext context, ILogger<SongService> logger, ISongFileService songFileService)
        {
            _context = context;
            _logger = logger;
            _songFileService = songFileService;
        }

        public async Task<SongResponse?> GetSongByIdAsync(Guid id)
        {
            try
            {
                var song = await _context.Songs
                    .Include(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                    .Where(s => s.IsActive && s.Id == id)
                    .FirstOrDefaultAsync();

                if (song == null)
                    return null;

                return new SongResponse(
                    song,
                    song.SongAuthors.Where(sa => sa.User.Role == UserRole.Author).Select(sa => new AuthorResponse(
                        sa.User.Id,
                        sa.User.Name,
                        sa.User.Bio,
                        sa.User.ImageUrl
                    )).ToList()
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving song {SongId}", id);
                throw;
            }
        }

        public async Task<(SongResponse? Song, string? ErrorMessage)> CreateSongAsync(CreateSongRequest request, Guid? authorId = null, bool isAdmin = false)
        {
            try
            {
                if (!isAdmin && (request.AuthorIds == null || !request.AuthorIds.Contains(authorId ?? Guid.Empty)))
                {
                    return (null, "Authors can only create songs for themselves.");
                }

                var audioExists = await _songFileService.FileExistsAsync(request.AudioId);
                if (!audioExists)
                {
                    return (null, $"Audio file not found for upload ID: {request.AudioId}");
                }

                var imageExists = await _songFileService.FileExistsAsync(request.ImageId);
                if (!imageExists)
                {
                    return (null, $"Image file not found for upload ID: {request.ImageId}");
                }

                var album = await _context.Playlists
                    .Where(p => p.Id == request.Album && p.IsAlbum)
                    .FirstOrDefaultAsync();

                if (album == null)
                {
                    return (null, $"Album with ID {request.Album} not found or is not an album.");
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
                        return (null, $"The following author IDs do not exist or are not authors: {string.Join(", ", invalidAuthorIds)}");
                    }

                    validatedAuthorIds = existingAuthorIds;
                }

                int audioDuration;
                try
                {
                    audioDuration = await _songFileService.GetAudioDurationAsync(request.AudioId);
                    if (audioDuration <= 0)
                    {
                        _logger.LogWarning("Could not determine audio duration for upload ID: {AudioId}", request.AudioId);
                        return (null, "Unable to determine audio file duration. The file may be corrupted or in an unsupported format.");
                    }
                    _logger.LogInformation("Audio duration: {Duration} seconds", audioDuration);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read audio duration for upload ID: {AudioId}", request.AudioId);
                    return (null, $"Failed to read audio file duration: {ex.Message}");
                }

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
                    return (null, $"Failed to process audio file: {ex.Message}");
                }

                try
                {
                    imageFilePath = await _songFileService.MoveUploadedFileAsync(request.ImageId, "images");
                    _logger.LogInformation("Moved image file to: {ImagePath}", imageFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move image file for upload ID: {ImageId}", request.ImageId);
                    return (null, $"Failed to process image file: {ex.Message}");
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
                    Duration = new Duration(audioDuration),
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

                var playlistSong = new PlaylistSong
                {
                    PlaylistId = request.Album,
                    SongId = newSong.Id,
                    Order = 0,
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
                    return (null, "Failed to retrieve created song");
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

                return (songResponse, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating song {SongName}", request.Name);
                throw;
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateSongAsync(Guid id, UpdateSongRequest request, Guid? userId = null, bool isAdmin = false)
        {
            try
            {
                var existing = await _context.Songs
                    .Include(s => s.SongAuthors)
                    .FirstOrDefaultAsync(s => s.Id == id && (isAdmin ||
                        s.SongAuthors.Any(sa => sa.UserId == userId)));

                if (existing == null)
                    return (false, null);

                if (request.Album.HasValue)
                {
                    // Check if album exists, it is album and author owns it
                    var album = await _context.Playlists
                        .Include(p => p.PlaylistOwners)
                        .Where(p => p.Id == request.Album.Value && p.IsAlbum &&
                            (isAdmin || p.PlaylistOwners.Any(po => po.UserId == userId)))
                        .FirstOrDefaultAsync();

                    if (album == null)
                        return (false, "Invalid album ID or you do not have permission to assign this album.");

                    if (existing.Album != request.Album.Value)
                    {
                        var oldPlaylistSong = await _context.PlaylistSongs
                            .FirstOrDefaultAsync(ps => ps.PlaylistId == existing.Album && ps.SongId == id);
                        if (oldPlaylistSong != null)
                        {
                            _context.PlaylistSongs.Remove(oldPlaylistSong);
                        }


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

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating song {SongId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteSongAsync(Guid id, Guid? userId = null, bool isAdmin = false)
        {
            try
            {
                var song = await _context.Songs
                    .Where(s => s.Id == id && (isAdmin ||
                        s.SongAuthors.Any(sa => sa.UserId == userId)))
                    .Include(s => s.SongAuthors)
                    .Include(s => s.PlaylistSongs)
                    .FirstOrDefaultAsync();

                if (song == null)
                    return false;

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

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting song {SongId}", id);
                throw;
            }
        }

        public async Task<List<SongSummaryResponse>> SearchSongsAsync(string query)
        {
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

                return songs.Select(s => new SongSummaryResponse(
                    s,
                    s.SongAuthors.Where(sa => sa.User.Role == UserRole.Author)
                        .Select(sa => sa.User.Name)
                        .ToList()
                )).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching songs with query: {Query}", query);
                throw;
            }
        }
    }
}
