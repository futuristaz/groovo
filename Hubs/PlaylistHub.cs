using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Models;

namespace Groovo.Hubs;

public class PlaylistHub : Hub
{
    private readonly ApplicationDbContext _dbContext;

    public PlaylistHub(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ----------------------
    // JOIN/LEAVE PLAYLIST GROUP
    // ----------------------
    public async Task JoinPlaylist(Guid playlistId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"playlist_{playlistId}");
        
        // Notify others in the group
        await Clients.OthersInGroup($"playlist_{playlistId}")
            .SendAsync("UserJoinedPlaylist", Context.ConnectionId);
    }

    public async Task LeavePlaylist(Guid playlistId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"playlist_{playlistId}");
        
        // Notify others in the group
        await Clients.OthersInGroup($"playlist_{playlistId}")
            .SendAsync("UserLeftPlaylist", Context.ConnectionId);
    }

    // ----------------------
    // CREATE PLAYLIST
    // ----------------------
    public async Task CreatePlaylist(CreatePlaylistRequest request)
    {
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Picture = request.Picture ?? string.Empty,
            IsPublic = request.IsPublic,
            IsAlbum = request.IsAlbum,
            IsActive = true,
            TotalDuration = 0  // Initialize with Duration struct
        };

        if (request.OwnerIds != null)
        {
            foreach (var userId in request.OwnerIds)
            {
                playlist.PlaylistOwners.Add(new PlaylistOwner
                {
                    PlaylistId = playlist.Id,
                    UserId = userId
                });
            }
        }

        _dbContext.Playlists.Add(playlist);
        await _dbContext.SaveChangesAsync();

        // Load navigation properties after save
        await _dbContext.Entry(playlist)
            .Collection(p => p.PlaylistOwners)
            .Query()
            .Include(po => po.User)
            .LoadAsync();

        var owners = playlist.PlaylistOwners
            .Select(po => new UserSummaryResponse(
                po.User.Id,
                po.User.Name,
                po.User.ImageUrl ?? string.Empty,
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
            new List<SongSummaryResponse>(), // empty initially
            owners
        );

        // Broadcast to all users (new playlist creation is global)
        await Clients.All.SendAsync("PlaylistCreated", response);
    }

    // ----------------------
    // ADD SONG TO PLAYLIST
    // ----------------------
    public async Task AddSongToPlaylist(AddSongRequest request)
    {
        // Include all necessary navigation properties
        var playlist = await _dbContext.Playlists
            .Include(p => p.PlaylistSongs)
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId);

        if (playlist == null)
            throw new HubException("Playlist not found");

        //  Include all necessary navigation properties for song
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
        playlist.TotalDuration += song.Duration;

        await _dbContext.SaveChangesAsync();

        var authors = song.SongAuthors
            .Select(sa => new AuthorResponse(
                sa.User.Id,
                sa.User.Name,
                sa.User.Bio ?? string.Empty,
                sa.User.ImageUrl ?? string.Empty
            ))
            .ToList();

        // Handle null tags and empty entries
        var tags = string.IsNullOrEmpty(song.Tags) 
            ? new List<string>() 
            : song.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToList();

        var response = new SongResponse(
            song,
            authors
        );

        // Broadcast only to users in this playlist group
        await Clients.Group($"playlist_{request.PlaylistId}")
            .SendAsync("SongAdded", request.PlaylistId, response);
    }

    // ----------------------
    // REMOVE SONG FROM PLAYLIST
    // ----------------------
    public async Task RemoveSongFromPlaylist(RemoveSongRequest request)
    {
        //  Include all necessary navigation properties
        var playlist = await _dbContext.Playlists
            .Include(p => p.PlaylistSongs)
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId);

        if (playlist == null)
            throw new HubException("Playlist not found");

        var playlistSong = playlist.PlaylistSongs
            .FirstOrDefault(ps => ps.SongId == request.SongId);
            
        if (playlistSong == null)
            throw new HubException("Song not found in playlist");

        // Load the song with all its navigation properties
        var song = await _dbContext.Songs
            .Include(s => s.SongAuthors)
                .ThenInclude(sa => sa.User)
            .FirstOrDefaultAsync(s => s.Id == request.SongId);

        if (song == null)
            throw new HubException("Song not found");

        var removedOrder = playlistSong.Order;

        _dbContext.PlaylistSongs.Remove(playlistSong);
        playlist.TotalDuration -= song.Duration;

        // Reorder remaining songs
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

        //  Handle null tags and empty entries
        var tags = string.IsNullOrEmpty(song.Tags) 
            ? new List<string>() 
            : song.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToList();

        var response = new SongResponse(
            song,
            authors
        );

        // Broadcast only to users in this playlist group
        await Clients.Group($"playlist_{request.PlaylistId}")
            .SendAsync("SongRemoved", request.PlaylistId, response);
    }

    // ----------------------
    // USER CONNECTION EVENTS
    // ----------------------
    public override async Task OnConnectedAsync()
    {
        // Broadcast to all users
        await Clients.All.SendAsync("UserJoined", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Broadcast to all users
        await Clients.All.SendAsync("UserLeft", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}