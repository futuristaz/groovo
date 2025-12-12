using Groovo.DTOs;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Models;
using Groovo.Repositories;

namespace Groovo.Services
{
    public class SongService : ISongService
    {
        private readonly ILogger<SongService> _logger;
        private readonly ISongFileService _songFileService;
        private readonly ISongRepository _songRepository;
        private readonly IPlaylistRepository _playlistRepository;
        private readonly IUserRepository _userRepository;

        public SongService(
            ILogger<SongService> logger, 
            ISongFileService songFileService,
            ISongRepository songRepository,
            IPlaylistRepository playlistRepository,
            IUserRepository userRepository)
        {
            _logger = logger;
            _songFileService = songFileService;
            _songRepository = songRepository;
            _playlistRepository = playlistRepository;
            _userRepository = userRepository;
        }

        public async Task<SongResponse?> GetSongByIdAsync(Guid id)
        {
            try
            {
                var song = await _songRepository.GetByIdAsync(id, includeAuthors: true);

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

                var album = await _playlistRepository.GetByIdAsync(request.Album);

                if (album == null || !album.IsAlbum)
                {
                    return (null, $"Album with ID {request.Album} not found or is not an album.");
                }

                var duplicateSong = await _songRepository.ExistsByNameAndAlbumAsync(request.Name, request.Album);
                if (duplicateSong)
                {
                    return (null, $"A song with the name '{request.Name}' already exists in this album.");
                }

                List<Guid> validatedAuthorIds = new List<Guid>();
                if (request.AuthorIds != null && request.AuthorIds.Any())
                {
                    var existingAuthors = await _userRepository.GetByIdsAsync(request.AuthorIds);
                    var existingAuthorIds = existingAuthors
                        .Where(u => u.Role == UserRole.Author)
                        .Select(u => u.Id)
                        .ToList();

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
                var songId = Guid.NewGuid();

                try
                {
                    audioFilePath = await _songFileService.MoveUploadedFileAsync(request.AudioId, "audio", songId.ToString());
                    _logger.LogInformation("Moved audio file to: {AudioPath}", audioFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move audio file for upload ID: {AudioId}", request.AudioId);
                    return (null, $"Failed to process audio file: {ex.Message}");
                }

                try
                {
                    imageFilePath = await _songFileService.MoveUploadedFileAsync(request.ImageId, "images", songId.ToString());
                    _logger.LogInformation("Moved image file to: {ImagePath}", imageFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move image file for upload ID: {ImageId}", request.ImageId);
                    return (null, $"Failed to process image file: {ex.Message}");
                }

                var newSong = new Song
                {
                    Id = songId,
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

                if (validatedAuthorIds.Any())
                {
                    var songAuthors = validatedAuthorIds.Select(authorId => new SongAuthor
                    {
                        SongId = newSong.Id,
                        UserId = authorId
                    }).ToList();

                    newSong.SongAuthors = songAuthors;
                }

                await _songRepository.CreateAsync(newSong);

                // Get the current max order in the album and add song
                var maxOrder = await _playlistRepository.GetMaxSongOrderAsync(request.Album);
                await _playlistRepository.AddSongToPlaylistAsync(request.Album, newSong.Id, maxOrder + 1);

                var createdSong = await _songRepository.GetByIdAsync(newSong.Id, includeAuthors: true);

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
                var existing = await _songRepository.GetByIdAsync(id, includeInactive: true, includeAuthors: true);

                if (existing == null)
                    return (false, null);

                // Check permissions
                if (!isAdmin && !existing.SongAuthors.Any(sa => sa.UserId == userId))
                    return (false, null);

                if (request.Album.HasValue)
                {
                    // Check if album exists, it is album and author owns it
                    var album = await _playlistRepository.GetByIdAsync(request.Album.Value, includeOwners: true);

                    if (album == null || !album.IsAlbum)
                        return (false, "Invalid album ID or you do not have permission to assign this album.");

                    if (!isAdmin && !album.PlaylistOwners.Any(po => po.UserId == userId))
                        return (false, "Invalid album ID or you do not have permission to assign this album.");

                    if (existing.Album != request.Album.Value)
                    {
                        await _playlistRepository.RemoveSongFromPlaylistAsync(existing.Album, id);
                        var maxOrder = await _playlistRepository.GetMaxSongOrderAsync(request.Album.Value);
                        await _playlistRepository.AddSongToPlaylistAsync(request.Album.Value, id, maxOrder + 1);
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

                await _songRepository.UpdateAsync(existing);

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
                var song = await _songRepository.GetByIdAsync(id, includeInactive: true, includeAuthors: true);

                if (song == null)
                    return false;

                // Check permissions
                if (!isAdmin && !song.SongAuthors.Any(sa => sa.UserId == userId))
                    return false;

                // Delete song - related entities (SongAuthors, PlaylistSongs) will be cascade deleted
                await _songRepository.DeleteAsync(id);

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
                var songs = await _songRepository.SearchAsync(query);

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
