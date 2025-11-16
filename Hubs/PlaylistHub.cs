using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Models;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Groovo.Hubs;

public class PlaylistHub : Hub
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PlaylistHub> _logger;
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _playlistLocks = new();

    public PlaylistHub(ApplicationDbContext dbContext, ILogger<PlaylistHub> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    private SemaphoreSlim GetPlaylistLock(Guid playlistId)
    {
        return _playlistLocks.GetOrAdd(playlistId, _ => new SemaphoreSlim(1, 1));
    }

    private Guid GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new HubException("Unauthorized: Invalid user token");
        }
        return userId;
    }

    private async Task<UserRole> GetUserRole()
    {
        var userId = GetUserId();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            throw new HubException("User not found");
        }
        return user.Role;
    }

    private async Task<bool> IsPlaylistOwner(Guid playlistId, Guid userId)
    {
        return await _dbContext.PlaylistOwners
            .AnyAsync(po => po.PlaylistId == playlistId && po.UserId == userId);
    }

    private async Task<bool> CanAccessPlaylist(Guid playlistId, Guid userId)
    {
        var playlist = await _dbContext.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId);

        if (playlist == null) return false;
        if (playlist.IsPublic) return true;

        return await IsPlaylistOwner(playlistId, userId);
    }

    public async Task JoinPlaylist(Guid playlistId)
    {
        try
        {
            var userId = GetUserId();

            if (!await CanAccessPlaylist(playlistId, userId))
            {
                throw new HubException("Unauthorized: Cannot access this playlist");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"playlist_{playlistId}");
            
            await Clients.OthersInGroup($"playlist_{playlistId}")
                .SendAsync("UserJoinedPlaylist", Context.ConnectionId);
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining playlist {PlaylistId}", playlistId);
            throw new HubException("Failed to join playlist");
        }
    }

    public async Task LeavePlaylist(Guid playlistId)
    {
        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"playlist_{playlistId}");
            
            await Clients.OthersInGroup($"playlist_{playlistId}")
                .SendAsync("UserLeftPlaylist", Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving playlist {PlaylistId}", playlistId);
            throw new HubException("Failed to leave playlist");
        }
    }

    public async Task CreatePlaylist(CreatePlaylistRequest request)
    {
        try
        {
            var userId = GetUserId();

            var playlist = new Playlist
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description ?? string.Empty,
                Picture = request.Picture ?? string.Empty,
                IsPublic = request.IsPublic,
                IsAlbum = request.IsAlbum,
                IsActive = true
            };

            var ownerIds = request.OwnerIds ?? new List<Guid> { userId };
            
            if (!ownerIds.Contains(userId))
            {
                ownerIds.Add(userId);
            }

            foreach (var ownerId in ownerIds)
            {
                var userExists = await _dbContext.Users.AnyAsync(u => u.Id == ownerId);
                if (!userExists)
                {
                    throw new HubException($"User {ownerId} not found");
                }

                playlist.PlaylistOwners.Add(new PlaylistOwner
                {
                    PlaylistId = playlist.Id,
                    UserId = ownerId
                });
            }

            _dbContext.Playlists.Add(playlist);
            await _dbContext.SaveChangesAsync();

            await _dbContext.Entry(playlist)
                .Collection(p => p.PlaylistOwners)
                .Query()
                .Include(po => po.User)
                .LoadAsync();

            var owners = playlist.PlaylistOwners
                .Select(po => new UserSummaryResponse(
                    po.User.Id,
                    po.User.Name,
                    po.User.Bio ?? string.Empty,
                    po.User.Role
                ))
                .ToList();

            var response = new PlaylistResponse(
                playlist.Id,
                playlist.Name,
                playlist.Description,
                playlist.Picture,
                playlist.IsPublic,
                playlist.IsAlbum,
                playlist.IsActive,
                playlist.CreatedAt,
                playlist.UpdatedAt,
                playlist.TotalTime,
                playlist.PlaylistSongs.Count,
                new List<SongSummaryResponse>(),
                owners
            );

            if (playlist.IsPublic)
            {
                await Clients.All.SendAsync("PlaylistCreated", response);
            }
            else
            {
                await Clients.Group($"playlist_{playlist.Id}").SendAsync("PlaylistCreated", response);
            }
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playlist");
            throw new HubException("Failed to create playlist");
        }
    }

    public async Task AddSongToPlaylist(AddSongRequest request)
    {
        var playlistLock = GetPlaylistLock(request.PlaylistId);
        await playlistLock.WaitAsync();
        try
        {
            var userId = GetUserId();

            if (!await IsPlaylistOwner(request.PlaylistId, userId))
            {
                throw new HubException("Unauthorized: Only playlist owners can add songs");
            }

            var playlist = await _dbContext.Playlists
                .Include(p => p.PlaylistSongs)
                .FirstOrDefaultAsync(p => p.Id == request.PlaylistId);

            if (playlist == null)
                throw new HubException("Playlist not found");

            var song = await _dbContext.Songs
                .Include(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                .FirstOrDefaultAsync(s => s.Id == request.SongId);

            if (song == null)
                throw new HubException("Song not found");

            if (playlist.PlaylistSongs.Any(ps => ps.SongId == request.SongId))
                throw new HubException("Song already in playlist");

            var playlistSong = new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = song.Id,
                Order = playlist.PlaylistSongs.Count
            };

            _dbContext.PlaylistSongs.Add(playlistSong);
            playlist.TotalTime += song.Length;

            await _dbContext.SaveChangesAsync();

            var authors = song.SongAuthors
                .Select(sa => new AuthorResponse(
                    sa.User.Id,
                    sa.User.Name,
                    sa.User.Bio ?? string.Empty,
                    sa.User.ImageUrl ?? string.Empty
                ))
                .ToList();

            var response = new SongResponse(song, authors);

            await Clients.Group($"playlist_{request.PlaylistId}")
                .SendAsync("SongAdded", request.PlaylistId, response);
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding song to playlist {PlaylistId}", request.PlaylistId);
            throw new HubException("Failed to add song to playlist");
        }
        finally
        {
            playlistLock.Release();
        }
    }

    public async Task RemoveSongFromPlaylist(RemoveSongRequest request)
    {
        var playlistLock = GetPlaylistLock(request.PlaylistId);
        await playlistLock.WaitAsync();
        try
        {
            var userId = GetUserId();

            if (!await IsPlaylistOwner(request.PlaylistId, userId))
            {
                throw new HubException("Unauthorized: Only playlist owners can remove songs");
            }

            var playlist = await _dbContext.Playlists
                .Include(p => p.PlaylistSongs)
                .FirstOrDefaultAsync(p => p.Id == request.PlaylistId);

            if (playlist == null)
                throw new HubException("Playlist not found");

            var playlistSong = playlist.PlaylistSongs
                .FirstOrDefault(ps => ps.SongId == request.SongId);
                
            if (playlistSong == null)
                throw new HubException("Song not found in playlist");

            var song = await _dbContext.Songs
                .Include(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                .FirstOrDefaultAsync(s => s.Id == request.SongId);

            if (song == null)
                throw new HubException("Song not found");

            var removedOrder = playlistSong.Order;

            _dbContext.PlaylistSongs.Remove(playlistSong);
            playlist.TotalTime -= song.Length;
            if (playlist.TotalTime < 0) playlist.TotalTime = 0;

            var songsToReorder = playlist.PlaylistSongs
                .Where(ps => ps.Order > removedOrder)
                .ToList();

            foreach (var ps in songsToReorder)
            {
                ps.Order--;
            }

            await _dbContext.SaveChangesAsync();

            var authors = song.SongAuthors
                .Select(sa => new AuthorResponse(
                    sa.User.Id,
                    sa.User.Name,
                    sa.User.Bio ?? string.Empty,
                    sa.User.ImageUrl ?? string.Empty
                ))
                .ToList();

            var response = new SongResponse(song, authors);

            await Clients.Group($"playlist_{request.PlaylistId}")
                .SendAsync("SongRemoved", request.PlaylistId, response);
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing song from playlist {PlaylistId}", request.PlaylistId);
            throw new HubException("Failed to remove song from playlist");
        }
        finally
        {
            playlistLock.Release();
        }
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            await Clients.All.SendAsync("UserJoined", Context.ConnectionId);
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectedAsync");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            if (exception != null)
            {
                _logger.LogError(exception, "User disconnected with error");
            }
            await Clients.All.SendAsync("UserLeft", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDisconnectedAsync");
        }
    }
}