using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Groovo.Data.Contexts;
using Groovo.Services;
using Groovo.Models;
using Groovo.DTOs;
using Groovo.DTOs.Requests;
using Groovo.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Groovo.Tests.Services;

public class PlaylistServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<PlaylistService>> _mockLogger;
    private readonly Mock<IHubContext<PlaylistHub>> _mockHub;
    private readonly PlaylistService _service;

    public PlaylistServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<PlaylistService>>();
        _mockHub = new Mock<IHubContext<PlaylistHub>>();

        _service = new PlaylistService(_context, _mockLogger.Object, _mockHub.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAllPlaylistsAsync Tests

    [Fact]
    public async Task GetAllPlaylistsAsync_AsAdmin_ReturnsAllPlaylists()
    {
        // Arrange
        var publicPlaylist = CreateTestPlaylist("Public", isPublic: true);
        var privatePlaylist = CreateTestPlaylist("Private", isPublic: false);
        await _context.Playlists.AddRangeAsync(publicPlaylist, privatePlaylist);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllPlaylistsAsync(isAdmin: true);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "Public");
        Assert.Contains(result, p => p.Name == "Private");
    }

    [Fact]
    public async Task GetAllPlaylistsAsync_AsNonAdmin_ReturnsOnlyPublicPlaylists()
    {
        // Arrange
        var publicPlaylist = CreateTestPlaylist("Public", isPublic: true);
        var privatePlaylist = CreateTestPlaylist("Private", isPublic: false);
        await _context.Playlists.AddRangeAsync(publicPlaylist, privatePlaylist);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllPlaylistsAsync(isAdmin: false);

        // Assert
        Assert.Single(result);
        Assert.Equal("Public", result[0].Name);
    }

    [Fact]
    public async Task GetAllPlaylistsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetAllPlaylistsAsync(isAdmin: false);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region GetPlaylistByIdAsync Tests

    [Fact]
    public async Task GetPlaylistByIdAsync_PublicPlaylist_ReturnsPlaylist()
    {
        // Arrange
        var playlist = CreateTestPlaylist("Test", isPublic: true);
        await _context.Playlists.AddAsync(playlist);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPlaylistByIdAsync(playlist.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(playlist.Id, result.Id);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public async Task GetPlaylistByIdAsync_PrivatePlaylistAsOwner_ReturnsPlaylist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Private", isPublic: false);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };
        
        await _context.Users.AddAsync(user);
        await _context.Playlists.AddAsync(playlist);
        await _context.PlaylistOwners.AddAsync(playlistOwner);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPlaylistByIdAsync(playlist.Id, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(playlist.Id, result.Id);
    }

    [Fact]
    public async Task GetPlaylistByIdAsync_PrivatePlaylistAsNonOwner_ReturnsNull()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var owner = CreateTestUser(ownerId, "Owner");
        var playlist = CreateTestPlaylist("Private", isPublic: false);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = ownerId, User = owner };

        await _context.Users.AddAsync(owner);
        await _context.Playlists.AddAsync(playlist);
        await _context.PlaylistOwners.AddAsync(playlistOwner);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPlaylistByIdAsync(playlist.Id, otherUserId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPlaylistByIdAsync_NonExistentPlaylist_ReturnsNull()
    {
        // Act
        var result = await _service.GetPlaylistByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CreatePlaylistAsync Tests

    [Fact]
    public async Task CreatePlaylistAsync_ValidRequest_CreatesPlaylist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreatePlaylistRequest
        {
            Name = "New Playlist",
            Description = "Test Description",
            IsPublic = true,
            IsAlbum = false
        };

        // Act
        var (playlist, error) = await _service.CreatePlaylistAsync(request, userId, "User");

        // Assert
        Assert.NotNull(playlist);
        Assert.Null(error);
        Assert.Equal("New Playlist", playlist.Name);
        Assert.Equal("Test Description", playlist.Description);
        Assert.True(playlist.IsPublic);
        Assert.False(playlist.IsAlbum);
    }

    [Fact]
    public async Task CreatePlaylistAsync_AuthorCreatingPlaylist_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreatePlaylistRequest
        {
            Name = "Playlist",
            IsAlbum = false
        };

        // Act
        var (playlist, error) = await _service.CreatePlaylistAsync(request, userId, "Author");

        // Assert
        Assert.Null(playlist);
        Assert.Equal("Authors can only create albums.", error);
    }

    [Fact]
    public async Task CreatePlaylistAsync_UserCreatingAlbum_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreatePlaylistRequest
        {
            Name = "Album",
            IsAlbum = true
        };

        // Act
        var (playlist, error) = await _service.CreatePlaylistAsync(request, userId, "User");

        // Assert
        Assert.Null(playlist);
        Assert.Equal("Regular users cannot create albums.", error);
    }

    [Fact]
    public async Task CreatePlaylistAsync_AdminCanCreateAnything_Success()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreatePlaylistRequest
        {
            Name = "Admin Album",
            IsAlbum = true
        };

        // Act
        var (playlist, error) = await _service.CreatePlaylistAsync(request, userId, "Admin");

        // Assert
        Assert.NotNull(playlist);
        Assert.Null(error);
        Assert.True(playlist.IsAlbum);
    }

    [Fact]
    public async Task CreatePlaylistAsync_WithInvalidOwnerIds_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invalidOwnerId = Guid.NewGuid();
        var request = new CreatePlaylistRequest
        {
            Name = "Playlist",
            IsAlbum = false,
            OwnerIds = new List<Guid> { invalidOwnerId }
        };

        // Act
        var (playlist, error) = await _service.CreatePlaylistAsync(request, userId, "User");

        // Assert
        Assert.Null(playlist);
        Assert.Contains("do not exist", error);
    }

    [Fact]
    public async Task CreatePlaylistAsync_WithValidOwnerIds_CreatesPlaylistWithOwners()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "TestUser");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new CreatePlaylistRequest
        {
            Name = "Playlist",
            IsAlbum = false,
            OwnerIds = new List<Guid> { userId }
        };

        // Act
        var (playlist, error) = await _service.CreatePlaylistAsync(request, userId, "User");

        // Assert
        Assert.NotNull(playlist);
        Assert.Null(error);
        Assert.Single(playlist.Owners);
        Assert.Equal(userId, playlist.Owners[0].Id);
    }

    #endregion

    #region UpdatePlaylistAsync Tests

    [Fact]
    public async Task UpdatePlaylistAsync_AsOwner_UpdatesPlaylist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Original", isPublic: true);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };

        await _context.Users.AddAsync(user);
        await _context.Playlists.AddAsync(playlist);
        await _context.PlaylistOwners.AddAsync(playlistOwner);
        await _context.SaveChangesAsync();

        var request = new UpdatePlaylistRequest
        {
            Name = "Updated",
            Description = "New Description",
            IsPublic = false
        };

        // Act
        var (success, error) = await _service.UpdatePlaylistAsync(playlist.Id, request, userId);

        // Assert
        Assert.True(success);
        Assert.Null(error);

        var updated = await _context.Playlists.FindAsync(playlist.Id);
        Assert.Equal("Updated", updated!.Name);
        Assert.Equal("New Description", updated.Description);
        Assert.False(updated.IsPublic);
    }

    [Fact]
    public async Task UpdatePlaylistAsync_AsNonOwner_ReturnsFalse()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var owner = CreateTestUser(ownerId, "Owner");
        var playlist = CreateTestPlaylist("Test", isPublic: true);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = ownerId, User = owner };

        await _context.Users.AddAsync(owner);
        await _context.Playlists.AddAsync(playlist);
        await _context.PlaylistOwners.AddAsync(playlistOwner);
        await _context.SaveChangesAsync();

        var request = new UpdatePlaylistRequest { Name = "Updated" };

        // Act
        var (success, error) = await _service.UpdatePlaylistAsync(playlist.Id, request, otherUserId);

        // Assert
        Assert.False(success);
    }

    [Fact]
    public async Task UpdatePlaylistAsync_NonExistentPlaylist_ReturnsFalse()
    {
        // Arrange
        var request = new UpdatePlaylistRequest { Name = "Updated" };

        // Act
        var (success, error) = await _service.UpdatePlaylistAsync(Guid.NewGuid(), request, Guid.NewGuid());

        // Assert
        Assert.False(success);
    }

    #endregion

    #region DeletePlaylistAsync Tests

    [Fact]
    public async Task DeletePlaylistAsync_EmptyPlaylist_DeletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("ToDelete", isPublic: true);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };

        await _context.Users.AddAsync(user);
        await _context.Playlists.AddAsync(playlist);
        await _context.PlaylistOwners.AddAsync(playlistOwner);
        await _context.SaveChangesAsync();

        // Act
        var (success, error) = await _service.DeletePlaylistAsync(playlist.Id, userId);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        Assert.Null(await _context.Playlists.FindAsync(playlist.Id));
    }

    [Fact]
    public async Task DeletePlaylistAsync_AlbumWithSongs_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Album", isPublic: true, isAlbum: true);
        var song = CreateTestSong("Song", new Duration(180));
        var playlistSong = new PlaylistSong { PlaylistId = playlist.Id, SongId = song.Id };
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };

        await _context.Users.AddAsync(user);
        await _context.Playlists.AddAsync(playlist);
        await _context.Songs.AddAsync(song);
        await _context.PlaylistSongs.AddAsync(playlistSong);
        await _context.PlaylistOwners.AddAsync(playlistOwner);
        await _context.SaveChangesAsync();

        // Act
        var (success, error) = await _service.DeletePlaylistAsync(playlist.Id, userId);

        // Assert
        Assert.False(success);
        Assert.Equal("Cannot delete an album that contains songs.", error);
    }

    [Fact]
    public async Task DeletePlaylistAsync_PlaylistWithSongs_DeletesPlaylistAndSongs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Playlist", isPublic: true, isAlbum: false);
        var song = CreateTestSong("Song", new Duration(180));
        var playlistSong = new PlaylistSong { PlaylistId = playlist.Id, SongId = song.Id };
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };

        await _context.Users.AddAsync(user);
        await _context.Playlists.AddAsync(playlist);
        await _context.Songs.AddAsync(song);
        await _context.PlaylistSongs.AddAsync(playlistSong);
        await _context.PlaylistOwners.AddAsync(playlistOwner);
        await _context.SaveChangesAsync();

        // Act
        var (success, error) = await _service.DeletePlaylistAsync(playlist.Id, userId);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        Assert.Null(await _context.Playlists.FindAsync(playlist.Id));
        Assert.Empty(_context.PlaylistSongs.Where(ps => ps.PlaylistId == playlist.Id));
    }

    #endregion

    #region AddSongToPlaylistAsync Tests

    [Fact]
    public async Task AddSongToPlaylistAsync_ValidSong_AddsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Playlist", isPublic: true, isAlbum: false);
        var song = CreateTestSong("Song", new Duration(180)); // 3 minutes
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };

        await _context.Users.AddAsync(user);
        await _context.Playlists.AddAsync(playlist);
        await _context.Songs.AddAsync(song);
        await _context.PlaylistOwners.AddAsync(playlistOwner);
        await _context.SaveChangesAsync();

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

        // Act
        var (success, error) = await _service.AddSongToPlaylistAsync(playlist.Id, song.Id, userId);

        // Assert
        Assert.True(success);
        Assert.Contains("Added song", error);
        
        var updatedPlaylist = await _context.Playlists.Include(p => p.PlaylistSongs).FirstAsync(p => p.Id == playlist.Id);
        Assert.Single(updatedPlaylist.PlaylistSongs);
        Assert.Equal(180, updatedPlaylist.TotalDuration.TotalSeconds);
    }

    [Fact]
    public async Task AddSongToPlaylistAsync_DuplicateSong_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Playlist", isPublic: true, isAlbum: false);
        var song = CreateTestSong("Song", new Duration(180));
        var playlistSong = new PlaylistSong { PlaylistId = playlist.Id, SongId = song.Id };
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };

        await _context.Users.AddAsync(user);
        await _context.Playlists.AddAsync(playlist);
        await _context.Songs.AddAsync(song);
        await _context.PlaylistSongs.AddAsync(playlistSong);
        await _context.PlaylistOwners.AddAsync(playlistOwner);
        await _context.SaveChangesAsync();

        // Act
        var (success, error) = await _service.AddSongToPlaylistAsync(playlist.Id, song.Id, userId);

        // Assert
        Assert.False(success);
        Assert.Equal("Song already in playlist.", error);
    }

    [Fact]
    public async Task AddSongToPlaylistAsync_ToAlbum_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Album", isPublic: true, isAlbum: true);
        var song = CreateTestSong("Song", new Duration(180));
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };

        await _context.Users.AddAsync(user);
        await _context.Playlists.AddAsync(playlist);
        await _context.Songs.AddAsync(song);
        await _context.PlaylistOwners.AddAsync(playlistOwner);
        await _context.SaveChangesAsync();

        // Act
        var (success, error) = await _service.AddSongToPlaylistAsync(playlist.Id, song.Id, userId);

        // Assert
        Assert.False(success);
        Assert.Contains("not found", error);
    }

    #endregion

    #region RemoveSongFromPlaylistAsync Tests

    [Fact]
    public async Task RemoveSongFromPlaylistAsync_ExistingSong_RemovesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Playlist", isPublic: true, isAlbum: false);
        playlist.TotalDuration = new Duration(180); // 3 minutes
        var song = CreateTestSong("Song", new Duration(180));
        var playlistSong = new PlaylistSong { PlaylistId = playlist.Id, SongId = song.Id };
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };

        await _context.Users.AddAsync(user);
        await _context.Playlists.AddAsync(playlist);
        await _context.Songs.AddAsync(song);
        await _context.PlaylistSongs.AddAsync(playlistSong);
        await _context.PlaylistOwners.AddAsync(playlistOwner);
        await _context.SaveChangesAsync();

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

        // Act
        var (success, error) = await _service.RemoveSongFromPlaylistAsync(playlist.Id, song.Id, userId);

        // Assert
        Assert.True(success);
        Assert.Contains("Removed song", error);
        
        var updatedPlaylist = await _context.Playlists.Include(p => p.PlaylistSongs).FirstAsync(p => p.Id == playlist.Id);
        Assert.Empty(updatedPlaylist.PlaylistSongs);
        Assert.Equal(0, updatedPlaylist.TotalDuration.TotalSeconds);
    }

    [Fact]
    public async Task RemoveSongFromPlaylistAsync_NonExistentSong_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Playlist", isPublic: true, isAlbum: false);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };

        await _context.Users.AddAsync(user);
        await _context.Playlists.AddAsync(playlist);
        await _context.PlaylistOwners.AddAsync(playlistOwner);
        await _context.SaveChangesAsync();

        // Act
        var (success, error) = await _service.RemoveSongFromPlaylistAsync(playlist.Id, Guid.NewGuid(), userId);

        // Assert
        Assert.False(success);
        Assert.Contains("not found in this playlist", error);
    }

    #endregion

    #region SearchPlaylistsAsync Tests

    [Fact]
    public async Task SearchPlaylistsAsync_MatchingName_ReturnsResults()
    {
        // Arrange
        var playlist1 = CreateTestPlaylist("Rock Playlist", isPublic: true);
        var playlist2 = CreateTestPlaylist("Jazz Playlist", isPublic: true);
        var playlist3 = CreateTestPlaylist("Classical", isPublic: true);

        await _context.Playlists.AddRangeAsync(playlist1, playlist2, playlist3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.SearchPlaylistsAsync("Playlist");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Contains("Playlist", p.Name));
    }

    [Fact]
    public async Task SearchPlaylistsAsync_MatchingDescription_ReturnsResults()
    {
        // Arrange
        var playlist1 = CreateTestPlaylist("List1", isPublic: true, description: "Best rock songs");
        var playlist2 = CreateTestPlaylist("List2", isPublic: true, description: "Best jazz songs");

        await _context.Playlists.AddRangeAsync(playlist1, playlist2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.SearchPlaylistsAsync("rock");

        // Assert
        Assert.Single(result);
        Assert.Equal("List1", result[0].Name);
    }

    [Fact]
    public async Task SearchPlaylistsAsync_OnlyPublicPlaylists_ReturnsOnlyPublic()
    {
        // Arrange
        var publicPlaylist = CreateTestPlaylist("Public Rock", isPublic: true);
        var privatePlaylist = CreateTestPlaylist("Private Rock", isPublic: false);

        await _context.Playlists.AddRangeAsync(publicPlaylist, privatePlaylist);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.SearchPlaylistsAsync("Rock");

        // Assert
        Assert.Single(result);
        Assert.Equal("Public Rock", result[0].Name);
    }

    #endregion

    #region Helper Methods

    private Playlist CreateTestPlaylist(string name, bool isPublic = true, bool isAlbum = false, string description = "")
    {
        return new Playlist
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Picture = "",
            IsPublic = isPublic,
            IsAlbum = isAlbum,
            IsActive = true,
            TotalDuration = new Duration(0),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private User CreateTestUser(Guid id, string name)
    {
        return new User
        {
            Id = id,
            Name = name,
            Email = $"{name}@test.com",
            Role = UserRole.User
        };
    }

    private Song CreateTestSong(string name, Duration duration)
    {
        return new Song
        {
            Id = Guid.NewGuid(),
            Name = name,
            Duration = duration,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion
}