using Microsoft.AspNetCore.SignalR;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Hubs;
using Groovo.Models;
using Groovo.Repositories;
using Groovo.DTOs;

namespace Groovo.Services
{
    public class PlaylistService : IPlaylistService
    {
        private readonly ILogger<PlaylistService> _logger;
        private readonly IHubContext<PlaylistHub> _hub;
        private readonly IPlaylistRepository _playlistRepository;
        private readonly ISongRepository _songRepository;
        private readonly IUserRepository _userRepository;

        public PlaylistService(
            ILogger<PlaylistService> logger, 
            IHubContext<PlaylistHub> hub,
            IPlaylistRepository playlistRepository,
            ISongRepository songRepository,
            IUserRepository userRepository)
        {
            _logger = logger;
            _hub = hub;
            _playlistRepository = playlistRepository;
            _songRepository = songRepository;
            _userRepository = userRepository;
        }

        public async Task<List<PlaylistSummaryResponse>> GetAllPlaylistsAsync(bool isAdmin)
        {
            try
            {
                var playlists = await _playlistRepository.GetAllAsync(includePrivate: isAdmin);

                return playlists.Select(p => new PlaylistSummaryResponse(
                    p.Id,
                    p.Name,
                    p.Description ?? "",
                    p.Picture ?? "",
                    p.IsPublic,
                    p.IsAlbum,
                    p.TotalTime,
                    p.PlaylistSongs?.Count ?? 0
                )).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving playlists");
                throw;
            }
        }

        public async Task<PlaylistResponse?> GetPlaylistByIdAsync(Guid id, Guid? userId = null, bool isAdmin = false)
        {
            try
            {
                var playlist = await _playlistRepository.GetByIdAsync(id, includeOwners: true, includeSongs: true);

                if (playlist == null)
                    return null;

                // Check permissions
                if (!isAdmin)
                {
                    bool hasAccess = playlist.IsPublic;
                    if (!hasAccess && userId.HasValue)
                    {
                        hasAccess = playlist.PlaylistOwners.Any(po => po.UserId == userId);
                    }
                    
                    if (!hasAccess)
                        return null;
                }

                return new PlaylistResponse(
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
                    playlist.PlaylistOwners?.Select(po => new UserSummaryResponse(
                        po.User.Id,
                        po.User.Name,
                        po.User.ImageUrl ?? "",
                        po.User.Role
                    )).ToList() ?? new List<UserSummaryResponse>()
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving playlist {PlaylistId}", id);
                throw;
            }
        }

        public async Task<(PlaylistResponse? Playlist, string? ErrorMessage)> CreatePlaylistAsync(CreatePlaylistRequest request, Guid currentUserId, string userRole)
        {
            try
            {
                if (userRole != "Admin")
                {
                    if (userRole == "Author" && !request.IsAlbum)
                    {
                        return (null, "Authors can only create albums.");
                    }
                    else if (userRole == "User" && request.IsAlbum)
                    {
                        return (null, "Regular users cannot create albums.");
                    }
                }

                List<Guid> validatedOwnerIds = new List<Guid>();
                if (request.OwnerIds != null && request.OwnerIds.Any())
                {
                    var existingUsers = await _userRepository.GetByIdsAsync(request.OwnerIds);
                    var existingUserIds = existingUsers.Select(u => u.Id).ToList();

                    var invalidUserIds = request.OwnerIds.Except(existingUserIds).ToList();
                    if (invalidUserIds.Any())
                    {
                        return (null, $"The following user IDs do not exist: {string.Join(", ", invalidUserIds)}");
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

                if (validatedOwnerIds.Any())
                {
                    var playlistOwners = validatedOwnerIds.Select(ownerId => new PlaylistOwner
                    {
                        PlaylistId = newPlaylist.Id,
                        UserId = ownerId
                    }).ToList();

                    newPlaylist.PlaylistOwners = playlistOwners;
                }

                await _playlistRepository.CreateAsync(newPlaylist);

                var createdPlaylist = await _playlistRepository.GetByIdAsync(newPlaylist.Id, includeOwners: true);

                if (createdPlaylist == null)
                {
                    _logger.LogError("Failed to retrieve the created playlist {PlaylistId}", newPlaylist.Id);
                    return (null, "Failed to retrieve created playlist");
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
                    0,
                    createdPlaylist.PlaylistOwners?.Select(po => new UserSummaryResponse(
                        po.User.Id,
                        po.User.Name,
                        po.User.ImageUrl ?? "",
                        po.User.Role
                    )).ToList() ?? new List<UserSummaryResponse>()
                );

                _logger.LogInformation("Created new playlist {PlaylistId}: {PlaylistName}", newPlaylist.Id, newPlaylist.Name);

                return (playlistResponse, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating playlist {PlaylistName}", request.Name);
                throw;
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdatePlaylistAsync(Guid id, UpdatePlaylistRequest request, Guid? userId = null, bool isAdmin = false)
        {
            try
            {
                var existing = await _playlistRepository.GetByIdAsync(id, includeOwners: true);

                if (existing == null)
                    return (false, null);

                // Check permissions
                if (!isAdmin && !existing.PlaylistOwners.Any(po => po.UserId == userId))
                    return (false, null);

                if (request.Name != null)
                    existing.Name = request.Name;
                if (request.Description != null)
                    existing.Description = request.Description;
                if (request.Picture != null)
                    existing.Picture = request.Picture;
                if (request.IsPublic.HasValue)
                    existing.IsPublic = request.IsPublic.Value;

                await _playlistRepository.UpdateAsync(existing);

                _logger.LogInformation("Updated playlist {PlaylistId}: {PlaylistName}", id, existing.Name);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating playlist {PlaylistId}", id);
                throw;
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> DeletePlaylistAsync(Guid id, Guid? userId = null, bool isAdmin = false)
        {
            try
            {
                var playlist = await _playlistRepository.GetByIdAsync(id, includeOwners: true, includeSongs: true);

                if (playlist == null)
                    return (false, null);

                // Check permissions
                if (!isAdmin && !playlist.PlaylistOwners.Any(po => po.UserId == userId))
                    return (false, null);

                // If it is album and has songs, prevent deletion
                if (playlist.IsAlbum && playlist.PlaylistSongs.Any())
                {
                    return (false, "Cannot delete an album that contains songs.");
                }

                // Delete playlist - related entities (PlaylistOwners, PlaylistSongs) will be cascade deleted
                await _playlistRepository.DeleteAsync(id);

                _logger.LogInformation("Deleted playlist {PlaylistId}: {PlaylistName} and all related entries", id, playlist.Name);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting playlist {PlaylistId}", id);
                throw;
            }
        }

        public async Task<List<SongSummaryResponse>?> GetSongsInPlaylistAsync(Guid id, Guid? userId = null, bool isAdmin = false)
        {
            try
            {
                var playlist = await _playlistRepository.GetByIdWithSongDetailsAsync(id);

                if (playlist == null)
                    return null;

                // Check permissions
                if (!isAdmin)
                {
                    bool hasAccess = playlist.IsPublic;
                    if (!hasAccess && userId.HasValue)
                    {
                        hasAccess = playlist.PlaylistOwners.Any(po => po.UserId == userId);
                    }
                    
                    if (!hasAccess)
                        return null;
                }

                return playlist.PlaylistSongs
                    .Where(ps => ps.Song.IsActive)
                    .OrderBy(ps => ps.Order)
                    .Select(ps => new SongSummaryResponse(
                        ps.Song,
                        ps.Song.SongAuthors?.Where(sa => sa.User.Role == UserRole.Author)
                            .Select(sa => sa.User.Name).ToList() ?? new List<string>()
                    )).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving songs for playlist {PlaylistId}", id);
                throw;
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> AddSongToPlaylistAsync(Guid playlistId, Guid songId, Guid? userId = null, bool isAdmin = false)
        {
            try
            {
                var playlist = await _playlistRepository.GetByIdAsync(playlistId, includeOwners: true, includeSongs: true);

                if (playlist == null || playlist.IsAlbum)
                    return (false, $"Playlist with ID {playlistId} not found.");

                // Check permissions
                if (!isAdmin && !playlist.PlaylistOwners.Any(po => po.UserId == userId))
                    return (false, $"Playlist with ID {playlistId} not found.");

                var song = await _songRepository.GetByIdAsync(songId);

                if (song == null)
                    return (false, $"Song with ID {songId} not found.");

                var existingEntry = playlist.PlaylistSongs.FirstOrDefault(ps => ps.SongId == songId);
                if (existingEntry != null)
                    return (false, "Song already in playlist.");

                var nextOrder = playlist.PlaylistSongs.Any()
                    ? playlist.PlaylistSongs.Max(ps => ps.Order) + 1
                    : 0;

                await _playlistRepository.AddSongToPlaylistAsync(playlistId, songId, nextOrder);

                playlist.TotalDuration += song.Duration;
                await _playlistRepository.UpdateAsync(playlist);

                var songResponse = new SongSummaryResponse(
                    song,
                    await _songRepository.GetSongAuthorNamesAsync(songId)
                );

                await _hub.Clients.Group($"playlist_{playlistId}")
                    .SendAsync("SongAdded", new
                    {
                        song = songResponse,
                        order = nextOrder
                    });

                _logger.LogInformation("Added song {SongId} to playlist {PlaylistId}", songId, playlistId);

                return (true, $"Added song '{song.Name}' to playlist '{playlist.Name}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding song {SongId} to playlist {PlaylistId}", songId, playlistId);
                throw;
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> RemoveSongFromPlaylistAsync(Guid playlistId, Guid songId, Guid? userId = null, bool isAdmin = false)
        {
            try
            {
                var playlist = await _playlistRepository.GetByIdAsync(playlistId, includeOwners: true, includeSongs: true);

                if (playlist == null || playlist.IsAlbum)
                    return (false, $"Playlist with ID {playlistId} not found.");

                // Check permissions
                if (!isAdmin && !playlist.PlaylistOwners.Any(po => po.UserId == userId))
                    return (false, $"Playlist with ID {playlistId} not found.");

                var playlistSong = playlist.PlaylistSongs.FirstOrDefault(ps => ps.SongId == songId);
                if (playlistSong == null)
                    return (false, $"Song with ID {songId} not found in this playlist.");

                var song = await _songRepository.GetByIdAsync(songId);

                await _playlistRepository.RemoveSongFromPlaylistAsync(playlistId, songId);

                if (song != null)
                {
                    playlist.TotalDuration -= song.Duration;
                    await _playlistRepository.UpdateAsync(playlist);
                }

                await _hub.Clients.Group($"playlist_{playlistId}").SendAsync("SongRemoved", songId);

                _logger.LogInformation("Removed song {SongId} from playlist {PlaylistId}", songId, playlistId);

                return (true, $"Removed song from playlist '{playlist.Name}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing song {SongId} from playlist {PlaylistId}", songId, playlistId);
                throw;
            }
        }

        public async Task<List<PlaylistSummaryResponse>> SearchPlaylistsAsync(string query)
        {
            try
            {
                var playlists = await _playlistRepository.SearchAsync(query);

                return playlists.Select(p => new PlaylistSummaryResponse(
                    p.Id,
                    p.Name,
                    p.Description ?? "",
                    p.Picture ?? "",
                    p.IsPublic,
                    p.IsAlbum,
                    p.TotalTime,
                    p.PlaylistSongs?.Count ?? 0
                )).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching playlists with query: {Query}", query);
                throw;
            }
        }
    }
}
