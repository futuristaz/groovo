using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Hubs;
using Groovo.Models;

namespace Groovo.Services
{
    public class PlaylistManagementService : IPlaylistManagementService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PlaylistManagementService> _logger;
        private readonly IHubContext<PlaylistHub> _hub;

        public PlaylistManagementService(ApplicationDbContext context, ILogger<PlaylistManagementService> logger, IHubContext<PlaylistHub> hub)
        {
            _context = context;
            _logger = logger;
            _hub = hub;
        }

        public async Task<List<PlaylistSummaryResponse>> GetAllPlaylistsAsync(bool isAdmin)
        {
            try
            {
                var playlists = await _context.Playlists
                    .Include(p => p.PlaylistSongs)
                    .Where(p => p.IsPublic || isAdmin)
                    .OrderBy(p => p.CreatedAt)
                    .ToListAsync();

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
                var query = _context.Playlists
                    .Include(p => p.PlaylistSongs)
                    .Include(p => p.PlaylistOwners)
                    .ThenInclude(po => po.User)
                    .Where(p => p.Id == id);

                if (!isAdmin && userId.HasValue)
                {
                    query = query.Where(p => p.IsPublic || p.PlaylistOwners.Any(po => po.UserId == userId));
                }
                else if (!isAdmin)
                {
                    query = query.Where(p => p.IsPublic);
                }

                var playlist = await query.FirstOrDefaultAsync();

                if (playlist == null)
                    return null;

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
                    var existingUserIds = await _context.Users
                        .Where(u => request.OwnerIds.Contains(u.Id))
                        .Select(u => u.Id)
                        .ToListAsync();

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

                _context.Playlists.Add(newPlaylist);

                if (validatedOwnerIds.Any())
                {
                    var playlistOwners = validatedOwnerIds.Select(ownerId => new PlaylistOwner
                    {
                        PlaylistId = newPlaylist.Id,
                        UserId = ownerId
                    }).ToList();

                    _context.PlaylistOwners.AddRange(playlistOwners);
                }

                await _context.SaveChangesAsync();

                var createdPlaylist = await _context.Playlists
                    .Include(p => p.PlaylistOwners)
                    .ThenInclude(po => po.User)
                    .FirstOrDefaultAsync(p => p.Id == newPlaylist.Id);

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
                var existing = await _context.Playlists
                    .Include(p => p.PlaylistOwners)
                    .FirstOrDefaultAsync(p => p.Id == id && (isAdmin ||
                        p.PlaylistOwners.Any(po => po.UserId == userId)));

                if (existing == null)
                    return (false, null);

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
                var playlist = await _context.Playlists
                    .Include(p => p.PlaylistSongs)
                    .Include(p => p.PlaylistOwners)
                    .FirstOrDefaultAsync(p => p.Id == id && (isAdmin ||
                        p.PlaylistOwners.Any(po => po.UserId == userId)));

                if (playlist == null)
                    return (false, null);

                // If it is album and has songs, prevent deletion
                if (playlist.IsAlbum && playlist.PlaylistSongs.Any())
                {
                    return (false, "Cannot delete an album that contains songs.");
                }

                if (playlist.PlaylistSongs.Any())
                {
                    _context.PlaylistSongs.RemoveRange(playlist.PlaylistSongs);
                }

                if (playlist.PlaylistOwners.Any())
                {
                    _context.PlaylistOwners.RemoveRange(playlist.PlaylistOwners);
                }

                _context.Playlists.Remove(playlist);

                await _context.SaveChangesAsync();

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
                var query = _context.Playlists
                    .Include(p => p.PlaylistSongs)
                    .ThenInclude(ps => ps.Song)
                    .ThenInclude(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                    .Include(p => p.PlaylistOwners)
                    .Where(p => p.Id == id);

                if (!isAdmin && userId.HasValue)
                {
                    query = query.Where(p => p.IsPublic || p.PlaylistOwners.Any(po => po.UserId == userId));
                }
                else if (!isAdmin)
                {
                    query = query.Where(p => p.IsPublic);
                }

                var playlist = await query.FirstOrDefaultAsync();

                if (playlist == null)
                    return null;

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
                var playlist = await _context.Playlists
                    .Where(p => p.Id == playlistId && !p.IsAlbum)
                    .Include(p => p.PlaylistSongs)
                    .Include(p => p.PlaylistOwners)
                    .FirstOrDefaultAsync(p => isAdmin ||
                        p.PlaylistOwners.Any(po => po.UserId == userId));

                if (playlist == null)
                    return (false, $"Playlist with ID {playlistId} not found.");

                var song = await _context.Songs
                    .FirstOrDefaultAsync(s => s.Id == songId && s.IsActive);

                if (song == null)
                    return (false, $"Song with ID {songId} not found.");

                var existingEntry = playlist.PlaylistSongs.FirstOrDefault(ps => ps.SongId == songId);
                if (existingEntry != null)
                    return (false, "Song already in playlist.");

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

                playlist.TotalDuration += song.Duration;

                await _context.SaveChangesAsync();

                var songResponse = new SongSummaryResponse(
                    song,
                    await _context.SongAuthors
                        .Where(sa => sa.SongId == songId)
                        .Include(sa => sa.User)
                        .Where(sa => sa.User.Role == UserRole.Author)
                        .Select(sa => sa.User.Name)
                        .ToListAsync()
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
                var playlist = await _context.Playlists
                    .Where(p => p.Id == playlistId && !p.IsAlbum)
                    .Include(p => p.PlaylistSongs)
                    .Include(p => p.PlaylistOwners)
                    .FirstOrDefaultAsync(p => isAdmin ||
                        p.PlaylistOwners.Any(po => po.UserId == userId));

                if (playlist == null)
                    return (false, $"Playlist with ID {playlistId} not found.");

                var playlistSong = playlist.PlaylistSongs.FirstOrDefault(ps => ps.SongId == songId);
                if (playlistSong == null)
                    return (false, $"Song with ID {songId} not found in this playlist.");

                var song = await _context.Songs.FirstOrDefaultAsync(s => s.Id == songId);

                _context.PlaylistSongs.Remove(playlistSong);

                if (song != null)
                {
                    playlist.TotalDuration -= song.Duration;
                }

                await _context.SaveChangesAsync();

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
                var playlists = await _context.Playlists
                    .Include(p => p.PlaylistSongs)
                    .Where(p => p.IsPublic && (
                        p.Name.Contains(query) ||
                        (p.Description != null && p.Description.Contains(query))
                    ))
                    .OrderBy(p => p.Name)
                    .ToListAsync();

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
