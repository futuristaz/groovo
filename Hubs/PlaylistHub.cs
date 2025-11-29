using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Models;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Groovo.DTOs;
using Groovo.Services.Hub;

namespace Groovo.Hubs;

[Authorize(Roles = "User")]
public class PlaylistHub : Hub
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PlaylistHub> _logger;
    private readonly IPlaybackStateStore<string, PlaybackState> _playbackStateStore;
    private readonly IUserPlaylistTracker<string, string> _userPlaylistTracker;
    private readonly IPlaylistService _playlistService;
    

    public PlaylistHub(
        ApplicationDbContext dbContext,
        ILogger<PlaylistHub> logger,
        IUserPlaylistTracker<string, string> userPlaylistTracker,
        IPlaybackStateStore<string, PlaybackState> playbackStateStore,
        IPlaylistService playlistService
    )
    {
        _dbContext = dbContext;
        _logger = logger;
        _userPlaylistTracker = userPlaylistTracker;
        _playbackStateStore = playbackStateStore;
        _playlistService = playlistService;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectedAsync");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var currentPlaylist = _userPlaylistTracker.GetPlaylist(connectionId);

        try
        {
            if (exception != null)
            {
                _logger.LogError(exception, "User disconnected with error");
            }
            
            if (currentPlaylist != null)
            {
                await Groups.RemoveFromGroupAsync(connectionId, currentPlaylist);
                _userPlaylistTracker.Remove(connectionId);
                await _playbackStateStore.DecrementUsers(currentPlaylist);
                _playbackStateStore.TryRemove(currentPlaylist);
            }

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDisconnectedAsync");
        }
    }

    public async Task JoinPlaylist(Guid playlistId)
    {
        try
        {
            var newGroupName = $"playlist_{playlistId}";
            var stringifiedPlaylistId = playlistId.ToString();
            var connectionId = Context.ConnectionId;
            
            var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new HubException("Unauthorized: Invalid user token");
            }

            if (!await _playlistService.CanAccessPlaylist(playlistId, userId))
            {
                throw new HubException("Unauthorized: Cannot access this playlist");
            }

            var currentPlaylist = _userPlaylistTracker.GetPlaylist(connectionId);
            if (currentPlaylist != null && currentPlaylist != stringifiedPlaylistId)
            {
                await Groups.RemoveFromGroupAsync(connectionId, currentPlaylist);
                _userPlaylistTracker.Remove(connectionId);
                await _playbackStateStore.DecrementUsers(currentPlaylist);
                _playbackStateStore.TryRemove(currentPlaylist);
            }

            await Groups.AddToGroupAsync(connectionId, newGroupName);
            _userPlaylistTracker.SetPlaylist(connectionId, stringifiedPlaylistId);
            await _playbackStateStore.IncrementUsers(stringifiedPlaylistId);
        }
        catch (HubException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining playlist {PlaylistId}", playlistId);
            throw new HubException("Failed to join playlist");
        }
    }

    public async Task LeavePlaylist()
    {
        var connectionId = Context.ConnectionId;
        var currentPlaylist = _userPlaylistTracker.GetPlaylist(connectionId);
        if (currentPlaylist == null)
        {
            throw new HubException("Not in any playlist");
        }

        try
        {
            _userPlaylistTracker.Remove(connectionId);
            await Groups.RemoveFromGroupAsync(connectionId, currentPlaylist);
            await _playbackStateStore.DecrementUsers(currentPlaylist);
            _playbackStateStore.TryRemove(currentPlaylist);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving playlist {PlaylistId}", currentPlaylist);
            throw new HubException("Failed to leave playlist");
        }
    }

    public async Task PlayPause(bool status)
    {
        var playlistId = _userPlaylistTracker.GetPlaylist(Context.ConnectionId);

        if (playlistId == null)
        {
            throw new HubException("Not in any playlist");
        }

        try
        {
            var state = _playbackStateStore.TryUpdate(playlistId, ps =>
            {
                if (ps.IsPlaying != status)
                {
                    if (ps.IsPlaying)
                    {
                        var timeSinceUpdate = (DateTime.UtcNow - ps.LastUpdated).TotalSeconds;
                        ps.CurrentPosition = Math.Min(ps.CurrentPosition + (int)timeSinceUpdate, ps.CurrentLength);
                    }
                    
                    ps.IsPlaying = status;
                    ps.LastUpdated = DateTime.UtcNow;
                }
                return ps;
            });

            await Clients.Group($"playlist_{playlistId}").SendAsync("PlaybackState", state);
        }
        catch (HubException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing playlist {PlaylistId}", playlistId);
            throw new HubException("Failed to pause playlist");
        }
    }

    public async Task PlaySong(Guid? songId)
    {
        var playlistId = _userPlaylistTracker.GetPlaylist(Context.ConnectionId);

        if (playlistId == null)
        {
            throw new HubException("Not in any playlist");
        }

        var state = _playbackStateStore.GetOrCreate(playlistId);

        songId ??= state.NextSongId;

        if (!songId.HasValue)
        {
            throw new HubException("No song specified to play");
        }

        var songLength = await _playlistService.GetSongLength(songId.Value);
        if (songLength == 0)
        {
            throw new HubException("Song not found");
        }

        try
        {
            var nextSongId = await _playlistService.GetNextSongId(Guid.Parse(playlistId), songId.Value);
            
            var newState = _playbackStateStore.TryUpdate(playlistId, ps =>
            {
                ps.CurrentSongId = songId.Value;
                ps.CurrentPosition = 0;
                ps.CurrentLength = songLength;
                ps.NextSongId = nextSongId;
                ps.IsPlaying = true;
                ps.LastUpdated = DateTime.UtcNow;
                return ps;
            });

            await Clients.Group($"playlist_{playlistId}").SendAsync("PlaybackState", newState.CurrentSongId);
        }
        catch (HubException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error skipping in playlist {PlaylistId}", playlistId);
            throw new HubException("Failed to skip song");
        }
    }

    public async Task Seek(int position)
    {
        if (position < 0)
        {
            throw new HubException("Position cannot be negative");
        }
        
        var playlistId = _userPlaylistTracker.GetPlaylist(Context.ConnectionId);

        if (playlistId == null)
        {
            throw new HubException("Not in any playlist");
        }

        var state = _playbackStateStore.GetOrCreate(playlistId);

        if (state.CurrentSongId == null)
        {
            throw new HubException("No song is currently playing");
        }

        if (position > state.CurrentLength)
        {
            throw new HubException("Position exceeds song length");
        }

        try
        {
            var updatedState = _playbackStateStore.TryUpdate(playlistId, ps =>
            {
                ps.CurrentPosition = position;
                ps.LastUpdated = DateTime.UtcNow;
                return ps;
            });

            await Clients.Group($"playlist_{playlistId}").SendAsync("PlaybackState", updatedState);
        }
        catch (HubException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeking in playlist {PlaylistId}", playlistId);
            throw new HubException("Failed to seek position");
        }
    }

    public async Task GetPlaybackState()
    {
        var playlistId = _userPlaylistTracker.GetPlaylist(Context.ConnectionId);

        if (playlistId == null)
        {
            throw new HubException("Not in any playlist");
        }

        try
        {
            var state = _playbackStateStore.GetOrCreate(playlistId);
            
            if (state.IsPlaying)
            {
                var timeSinceUpdate = (DateTime.UtcNow - state.LastUpdated).TotalSeconds;
                state.CurrentPosition = Math.Min(state.CurrentPosition + (int)timeSinceUpdate, state.CurrentLength);
            }

            await Clients.Caller.SendAsync("PlaybackState", state);
        }
        catch (HubException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting playback state for playlist {PlaylistId}", playlistId);
            throw new HubException("Failed to get playback state");
        }
    }
}