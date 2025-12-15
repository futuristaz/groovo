using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Groovo.Models;
using Groovo.DTOs;
using Groovo.DTOs.Requests;
using Groovo.Hubs;
using Groovo.Repositories;
using Groovo.Services;

namespace Groovo.Tests.Services;

public class PlaylistServiceTests
{
    private readonly Mock<ILogger<PlaylistService>> _mockLogger;
    private readonly Mock<IHubContext<PlaylistHub>> _mockHub;
    private readonly Mock<IPlaylistRepository> _playlistRepositoryMock;
    private readonly Mock<ISongRepository> _songRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly PlaylistService _service;

    public PlaylistServiceTests()
    {
        _mockLogger = new Mock<ILogger<PlaylistService>>();
        _mockHub = new Mock<IHubContext<PlaylistHub>>();
        _playlistRepositoryMock = new Mock<IPlaylistRepository>();
        _songRepositoryMock = new Mock<ISongRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();

        _service = new PlaylistService(
            _mockLogger.Object,
            _mockHub.Object,
            _playlistRepositoryMock.Object,
            _songRepositoryMock.Object,
            _userRepositoryMock.Object
        );
    }

    #region GetAllPlaylistsAsync Tests

    [Fact]
    public async Task GetAllPlaylistsAsync_AsAdmin_ReturnsAllPlaylists()
    {
        // Arrange
        var publicPlaylist = CreateTestPlaylist("Public", isPublic: true);
        var privatePlaylist = CreateTestPlaylist("Private", isPublic: false);
        var playlists = new List<Playlist> { publicPlaylist, privatePlaylist };

        _playlistRepositoryMock.Setup(r => r.GetAllAsync(true))
            .ReturnsAsync(playlists);

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
        var playlists = new List<Playlist> { publicPlaylist };

        _playlistRepositoryMock.Setup(r => r.GetAllAsync(false))
            .ReturnsAsync(playlists);

        // Act
        var result = await _service.GetAllPlaylistsAsync(isAdmin: false);

        // Assert
        Assert.Single(result);
        Assert.Equal("Public", result[0].Name);
    }

    [Fact]
    public async Task GetAllPlaylistsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        _playlistRepositoryMock.Setup(r => r.GetAllAsync(false))
            .ReturnsAsync(new List<Playlist>());

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
        playlist.PlaylistOwners = new List<PlaylistOwner>();

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

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
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

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
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        // Act
        var result = await _service.GetPlaylistByIdAsync(playlist.Id, otherUserId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPlaylistByIdAsync_NonExistentPlaylist_ReturnsNull()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
            .ReturnsAsync((Playlist?)null);

        // Act
        var result = await _service.GetPlaylistByIdAsync(playlistId);

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

        Playlist capturedPlaylist = null!;
        _playlistRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Playlist>()))
            .Callback<Playlist>(p => capturedPlaylist = p)
            .ReturnsAsync((Playlist p) => p);

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), true, false))
            .ReturnsAsync((Guid id, bool includeOwners, bool includeSongs) => 
            {
                var playlist = capturedPlaylist;
                playlist.PlaylistOwners = new List<PlaylistOwner>();
                return playlist;
            });

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
    public async Task CreatePlaylistAsync_WithInvalidOwnerIds_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invalidOwnerId = Guid.NewGuid();
        
        _userRepositoryMock.Setup(r => r.GetByIdsAsync(It.Is<List<Guid>>(ids => ids.Contains(invalidOwnerId))))
            .ReturnsAsync(new List<User>());

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

        _userRepositoryMock.Setup(r => r.GetByIdsAsync(It.Is<List<Guid>>(ids => ids.Contains(userId))))
            .ReturnsAsync(new List<User> { user });

        Playlist capturedPlaylist = null!;
        _playlistRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Playlist>()))
            .Callback<Playlist>(p => capturedPlaylist = p)
            .ReturnsAsync((Playlist p) => p);

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), true, false))
            .ReturnsAsync((Guid id, bool includeOwners, bool includeSongs) =>
            {
                var playlist = capturedPlaylist;
                playlist.PlaylistOwners = new List<PlaylistOwner> 
                { 
                    new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user }
                };
                return playlist;
            });

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
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, false))
            .ReturnsAsync(playlist);

        Playlist updatedPlaylist = null!;
        _playlistRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Playlist>()))
            .Callback<Playlist>(p => updatedPlaylist = p)
            .Returns(Task.CompletedTask);

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
        Assert.Equal("Updated", updatedPlaylist.Name);
        Assert.Equal("New Description", updatedPlaylist.Description);
        Assert.False(updatedPlaylist.IsPublic);
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
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, false))
            .ReturnsAsync(playlist);

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
        var playlistId = Guid.NewGuid();
        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, false))
            .ReturnsAsync((Playlist?)null);

        var request = new UpdatePlaylistRequest { Name = "Updated" };

        // Act
        var (success, error) = await _service.UpdatePlaylistAsync(playlistId, request, Guid.NewGuid());

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
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
        playlist.PlaylistSongs = new List<PlaylistSong>();

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        _playlistRepositoryMock.Setup(r => r.DeleteAsync(playlist.Id))
            .Returns(Task.CompletedTask);

        // Act
        var (success, error) = await _service.DeletePlaylistAsync(playlist.Id, userId);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        _playlistRepositoryMock.Verify(r => r.DeleteAsync(playlist.Id), Times.Once);
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
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
        playlist.PlaylistSongs = new List<PlaylistSong> { playlistSong };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

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
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
        playlist.PlaylistSongs = new List<PlaylistSong> { playlistSong };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        _playlistRepositoryMock.Setup(r => r.DeleteAsync(playlist.Id))
            .Returns(Task.CompletedTask);

        // Act
        var (success, error) = await _service.DeletePlaylistAsync(playlist.Id, userId);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        _playlistRepositoryMock.Verify(r => r.DeleteAsync(playlist.Id), Times.Once);
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
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, false, false, false))
            .ReturnsAsync(song);

        _playlistRepositoryMock.Setup(r => r.GetMaxSongOrderAsync(playlist.Id))
            .ReturnsAsync(0);

        _playlistRepositoryMock.Setup(r => r.AddSongToPlaylistAsync(playlist.Id, song.Id, 0))
            .ReturnsAsync(1);

        _songRepositoryMock.Setup(r => r.GetSongLengthAsync(song.Id))
            .ReturnsAsync(180);

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

        // Act
        var (success, error) = await _service.AddSongToPlaylistAsync(playlist.Id, song.Id, userId);

        // Assert
        Assert.True(success);
        Assert.Contains("Added song", error);
        _playlistRepositoryMock.Verify(r => r.AddSongToPlaylistAsync(playlist.Id, song.Id, 0), Times.Once);
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
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
        playlist.PlaylistSongs = new List<PlaylistSong> { playlistSong };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, false, false, false))
            .ReturnsAsync(song);

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
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, false))
            .ReturnsAsync(playlist);

        _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, false, false, false))
            .ReturnsAsync(song);

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
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
        playlist.PlaylistSongs = new List<PlaylistSong> { playlistSong };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        _playlistRepositoryMock.Setup(r => r.RemoveSongFromPlaylistAsync(playlist.Id, song.Id))
            .Returns(Task.CompletedTask);

        _songRepositoryMock.Setup(r => r.GetSongLengthAsync(song.Id))
            .ReturnsAsync(180);

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

        // Act
        var (success, error) = await _service.RemoveSongFromPlaylistAsync(playlist.Id, song.Id, userId);

        // Assert
        Assert.True(success);
        Assert.Contains("Removed song", error);
        _playlistRepositoryMock.Verify(r => r.RemoveSongFromPlaylistAsync(playlist.Id, song.Id), Times.Once);
    }

    [Fact]
    public async Task RemoveSongFromPlaylistAsync_NonExistentSong_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Playlist", isPublic: true, isAlbum: false);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
        playlist.PlaylistSongs = new List<PlaylistSong>();

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, false))
            .ReturnsAsync(playlist);

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, false, true))
            .ReturnsAsync(playlist);

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

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

        _playlistRepositoryMock.Setup(r => r.SearchAsync("Playlist"))
            .ReturnsAsync(new List<Playlist> { playlist1, playlist2 });

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

        _playlistRepositoryMock.Setup(r => r.SearchAsync("rock"))
            .ReturnsAsync(new List<Playlist> { playlist1 });

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

        _playlistRepositoryMock.Setup(r => r.SearchAsync("Rock"))
            .ReturnsAsync(new List<Playlist> { publicPlaylist });

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

    [Fact]
    public async Task UpdatePlaylistAsync_AsAdmin_UpdatesPlaylistWithoutOwnerCheck()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var owner = CreateTestUser(ownerId, "Owner");
        var playlist = CreateTestPlaylist("Original", isPublic: true);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlistId, UserId = ownerId, User = owner };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, false))
            .ReturnsAsync(playlist);

        Playlist updatedPlaylist = null!;
        _playlistRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Playlist>()))
            .Callback<Playlist>(p => updatedPlaylist = p)
            .Returns(Task.CompletedTask);

        var request = new UpdatePlaylistRequest { Name = "AdminUpdated" };

        // Act
        var (success, error) = await _service.UpdatePlaylistAsync(playlistId, request, otherUserId, isAdmin: true);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("AdminUpdated", updatedPlaylist.Name);
    }

    [Fact]
    public async Task UpdatePlaylistAsync_WithPartialUpdate_OnlyUpdatesProvidedFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Original", isPublic: true, description: "Original Description");
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, false))
            .ReturnsAsync(playlist);

        Playlist updatedPlaylist = null!;
        _playlistRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Playlist>()))
            .Callback<Playlist>(p => updatedPlaylist = p)
            .Returns(Task.CompletedTask);

        // Only update IsPublic, leave Name and Description unchanged
        var request = new UpdatePlaylistRequest { IsPublic = false };

        // Act
        var (success, error) = await _service.UpdatePlaylistAsync(playlist.Id, request, userId);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("Original Description", updatedPlaylist.Description);
        Assert.False(updatedPlaylist.IsPublic);
    }

    #endregion

    #region DeletePlaylistAsync Additional Tests

    [Fact]
    public async Task DeletePlaylistAsync_AsNonOwner_ReturnsFalse()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var owner = CreateTestUser(ownerId, "Owner");
        var playlist = CreateTestPlaylist("Test", isPublic: true);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = ownerId, User = owner };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
        playlist.PlaylistSongs = new List<PlaylistSong>();

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        // Act
        var (success, error) = await _service.DeletePlaylistAsync(playlist.Id, otherUserId);

        // Assert
        Assert.False(success);
    }

    [Fact]
    public async Task DeletePlaylistAsync_AsAdmin_DeletesWithoutOwnerCheck()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var owner = CreateTestUser(ownerId, "Owner");
        var playlist = CreateTestPlaylist("Test", isPublic: true);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = ownerId, User = owner };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
        playlist.PlaylistSongs = new List<PlaylistSong>();

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        _playlistRepositoryMock.Setup(r => r.DeleteAsync(playlist.Id))
            .Returns(Task.CompletedTask);

        // Act
        var (success, error) = await _service.DeletePlaylistAsync(playlist.Id, adminUserId, isAdmin: true);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        _playlistRepositoryMock.Verify(r => r.DeleteAsync(playlist.Id), Times.Once);
    }

    [Fact]
    public async Task DeletePlaylistAsync_NonExistentPlaylist_ReturnsFalse()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
            .ReturnsAsync((Playlist?)null);

        // Act
        var (success, error) = await _service.DeletePlaylistAsync(playlistId, Guid.NewGuid());

        // Assert
        Assert.False(success);
    }

    #endregion

    #region GetPlaylistByIdAsync with isAdmin Tests

    [Fact]
    public async Task GetPlaylistByIdAsync_PrivatePlaylistAsAdmin_ReturnsPlaylist()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Private", isPublic: false);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlistId, UserId = userId, User = user };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
            .ReturnsAsync(playlist);

        // Act
        var result = await _service.GetPlaylistByIdAsync(playlistId, Guid.NewGuid(), isAdmin: true);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetPlaylistByIdAsync_WithoutUserIdNonPublic_ReturnsNull()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Private", isPublic: false);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlistId, UserId = userId, User = user };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
            .ReturnsAsync(playlist);

        // Act
        var result = await _service.GetPlaylistByIdAsync(playlistId, userId: null);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region AddSongToPlaylistAsync Additional Tests

    [Fact]
    public async Task AddSongToPlaylistAsync_AsNonOwner_ReturnsError()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var owner = CreateTestUser(ownerId, "Owner");
        var playlist = CreateTestPlaylist("Playlist", isPublic: true);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = ownerId, User = owner };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
        playlist.PlaylistSongs = new List<PlaylistSong>();

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        // Act
        var (success, error) = await _service.AddSongToPlaylistAsync(playlist.Id, Guid.NewGuid(), otherUserId);

        // Assert
        Assert.False(success);
    }

    [Fact]
    public async Task AddSongToPlaylistAsync_NonExistentPlaylist_ReturnsError()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
            .ReturnsAsync((Playlist?)null);

        // Act
        var (success, error) = await _service.AddSongToPlaylistAsync(playlistId, songId, userId);

        // Assert
        Assert.False(success);
    }

    [Fact]
    public async Task AddSongToPlaylistAsync_NonExistentSong_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var playlist = CreateTestPlaylist("Playlist", isPublic: true);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        _songRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync((Song?)null);

        // Act
        var (success, error) = await _service.AddSongToPlaylistAsync(playlist.Id, Guid.NewGuid(), userId);

        // Assert
        Assert.False(success);
    }

    #endregion

    #region RemoveSongFromPlaylistAsync Additional Tests

    [Fact]
    public async Task RemoveSongFromPlaylistAsync_AsNonOwner_ReturnsError()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var owner = CreateTestUser(ownerId, "Owner");
        var playlist = CreateTestPlaylist("Playlist", isPublic: true);
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = ownerId, User = owner };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        // Act
        var (success, error) = await _service.RemoveSongFromPlaylistAsync(playlist.Id, Guid.NewGuid(), otherUserId);

        // Assert
        Assert.False(success);
    }

    [Fact]
    public async Task RemoveSongFromPlaylistAsync_NonExistentPlaylist_ReturnsError()
    {
        // Arrange
        var playlistId = Guid.NewGuid();

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
            .ReturnsAsync((Playlist?)null);

        // Act
        var (success, error) = await _service.RemoveSongFromPlaylistAsync(playlistId, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        Assert.False(success);
    }

    #endregion

    #region SearchPlaylistsAsync Additional Tests

    [Fact]
    public async Task SearchPlaylistsAsync_NoMatches_ReturnsEmptyList()
    {
        // Arrange
        _playlistRepositoryMock.Setup(r => r.SearchAsync("NonExistent"))
            .ReturnsAsync(new List<Playlist>());

        // Act
        var result = await _service.SearchPlaylistsAsync("NonExistent");

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region GetSongsInPlaylistAsync Tests

    [Fact]
    public async Task GetSongsInPlaylistAsync_PublicPlaylist_ReturnsActiveSongs()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var song1 = CreateTestSong("Active Song", new Duration(180));
        song1.IsActive = true;
        var song2 = CreateTestSong("Inactive Song", new Duration(120));
        song2.IsActive = false;

        var playlistSong1 = new PlaylistSong { PlaylistId = playlistId, SongId = song1.Id, Song = song1, Order = 0 };
        var playlistSong2 = new PlaylistSong { PlaylistId = playlistId, SongId = song2.Id, Song = song2, Order = 1 };

        var playlist = CreateTestPlaylist("Public", isPublic: true);
        playlist.Id = playlistId;
        playlist.PlaylistSongs = new List<PlaylistSong> { playlistSong1, playlistSong2 };
        playlist.PlaylistOwners = new List<PlaylistOwner>();

        _playlistRepositoryMock.Setup(r => r.GetByIdWithSongDetailsAsync(playlistId))
            .ReturnsAsync(playlist);

        // Act
        var result = await _service.GetSongsInPlaylistAsync(playlistId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Active Song", result[0].Name);
    }

    [Fact]
    public async Task GetSongsInPlaylistAsync_PrivatePlaylistAsOwner_ReturnsActiveSongs()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");

        var song = CreateTestSong("Song", new Duration(180));
        song.IsActive = true;

        var playlistSong = new PlaylistSong { PlaylistId = playlistId, SongId = song.Id, Song = song, Order = 0 };

        var playlist = CreateTestPlaylist("Private", isPublic: false);
        playlist.Id = playlistId;
        playlist.PlaylistSongs = new List<PlaylistSong> { playlistSong };
        var playlistOwner = new PlaylistOwner { PlaylistId = playlistId, UserId = userId, User = user };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdWithSongDetailsAsync(playlistId))
            .ReturnsAsync(playlist);

        // Act
        var result = await _service.GetSongsInPlaylistAsync(playlistId, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetSongsInPlaylistAsync_PrivatePlaylistAsNonOwner_ReturnsNull()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var owner = CreateTestUser(ownerId, "Owner");

        var playlist = CreateTestPlaylist("Private", isPublic: false);
        playlist.Id = playlistId;
        var playlistOwner = new PlaylistOwner { PlaylistId = playlistId, UserId = ownerId, User = owner };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdWithSongDetailsAsync(playlistId))
            .ReturnsAsync(playlist);

        // Act
        var result = await _service.GetSongsInPlaylistAsync(playlistId, otherUserId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSongsInPlaylistAsync_PrivatePlaylistAsAdmin_ReturnsActiveSongs()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var owner = CreateTestUser(ownerId, "Owner");

        var song = CreateTestSong("Song", new Duration(180));
        song.IsActive = true;

        var playlistSong = new PlaylistSong { PlaylistId = playlistId, SongId = song.Id, Song = song, Order = 0 };

        var playlist = CreateTestPlaylist("Private", isPublic: false);
        playlist.Id = playlistId;
        playlist.PlaylistSongs = new List<PlaylistSong> { playlistSong };
        var playlistOwner = new PlaylistOwner { PlaylistId = playlistId, UserId = ownerId, User = owner };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdWithSongDetailsAsync(playlistId))
            .ReturnsAsync(playlist);

        // Act
        var result = await _service.GetSongsInPlaylistAsync(playlistId, adminId, isAdmin: true);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetSongsInPlaylistAsync_NonExistentPlaylist_ReturnsNull()
    {
        // Arrange
        var playlistId = Guid.NewGuid();

        _playlistRepositoryMock.Setup(r => r.GetByIdWithSongDetailsAsync(playlistId))
            .ReturnsAsync((Playlist?)null);

        // Act
        var result = await _service.GetSongsInPlaylistAsync(playlistId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSongsInPlaylistAsync_EmptyPlaylist_ReturnsEmptyList()
    {
        // Arrange
        var playlistId = Guid.NewGuid();

        var playlist = CreateTestPlaylist("Empty", isPublic: true);
        playlist.Id = playlistId;
        playlist.PlaylistSongs = new List<PlaylistSong>();
        playlist.PlaylistOwners = new List<PlaylistOwner>();

        _playlistRepositoryMock.Setup(r => r.GetByIdWithSongDetailsAsync(playlistId))
            .ReturnsAsync(playlist);

        // Act
        var result = await _service.GetSongsInPlaylistAsync(playlistId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region AddSongToPlaylistAsync Album Tests

    [Fact]
    public async Task AddSongToPlaylistAsync_ToNonExistentAlbum_ReturnsError()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var songId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Author");

        var playlist = CreateTestPlaylist("Album", isPublic: true, isAlbum: true);
        playlist.Id = playlistId;
        var playlistOwner = new PlaylistOwner { PlaylistId = playlistId, UserId = userId, User = user };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
            .ReturnsAsync(playlist);

        // Act
        var (success, error) = await _service.AddSongToPlaylistAsync(playlistId, songId, userId);

        // Assert
        Assert.False(success);
        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task AddSongToPlaylistAsync_WithAdminOverride_AddsSuccessfully()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var owner = CreateTestUser(ownerId, "Owner");
        var playlist = CreateTestPlaylist("Playlist", isPublic: true);
        var song = CreateTestSong("Song", new Duration(180));
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = ownerId, User = owner };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
        playlist.PlaylistSongs = new List<PlaylistSong>();

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(song);

        _playlistRepositoryMock.Setup(r => r.GetMaxSongOrderAsync(playlist.Id))
            .ReturnsAsync(0);

        _playlistRepositoryMock.Setup(r => r.AddSongToPlaylistAsync(playlist.Id, song.Id, It.IsAny<int>()))
            .ReturnsAsync(1);

        _songRepositoryMock.Setup(r => r.GetSongLengthAsync(song.Id))
            .ReturnsAsync(180);

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

        // Act
        var (success, error) = await _service.AddSongToPlaylistAsync(playlist.Id, song.Id, adminId, isAdmin: true);

        // Assert
        Assert.True(success);
    }

    #endregion

    #region RemoveSongFromPlaylistAsync Album Tests

    [Fact]
    public async Task RemoveSongFromPlaylistAsync_FromAlbumFails_ReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "Owner");
        var album = CreateTestPlaylist("Album", isPublic: true, isAlbum: true);
        var playlistOwner = new PlaylistOwner { PlaylistId = album.Id, UserId = userId, User = user };
        album.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(album.Id, true, true))
            .ReturnsAsync(album);

        // Act
        var (success, error) = await _service.RemoveSongFromPlaylistAsync(album.Id, Guid.NewGuid(), userId);

        // Assert
        Assert.False(success);
        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task RemoveSongFromPlaylistAsync_WithAdminOverride_RemovesSuccessfully()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var owner = CreateTestUser(ownerId, "Owner");
        var playlist = CreateTestPlaylist("Playlist", isPublic: true);
        playlist.TotalDuration = new Duration(180);
        var song = CreateTestSong("Song", new Duration(180));
        var playlistSong = new PlaylistSong { PlaylistId = playlist.Id, SongId = song.Id };
        var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = ownerId, User = owner };
        playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
        playlist.PlaylistSongs = new List<PlaylistSong> { playlistSong };

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, true))
            .ReturnsAsync(playlist);

        _playlistRepositoryMock.Setup(r => r.RemoveSongFromPlaylistAsync(playlist.Id, song.Id))
            .Returns(Task.CompletedTask);

        _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(song);

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

        // Act
        var (success, error) = await _service.RemoveSongFromPlaylistAsync(playlist.Id, song.Id, adminId, isAdmin: true);

        // Assert
        Assert.True(success);
    }

    #endregion

#region CreatePlaylistAsync Additional Tests

[Fact]
public async Task CreatePlaylistAsync_AdminCanCreateAlbum_CreatesSuccessfully()
{
    // Arrange
    var userId = Guid.NewGuid();
    var request = new CreatePlaylistRequest
    {
        Name = "Admin Album",
        IsAlbum = true
    };

    Playlist capturedPlaylist = null!;
    _playlistRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Playlist>()))
        .Callback<Playlist>(p => capturedPlaylist = p)
        .ReturnsAsync((Playlist p) => p);

    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), true, false))
        .ReturnsAsync((Guid id, bool includeOwners, bool includeSongs) =>
        {
            var playlist = capturedPlaylist;
            playlist.PlaylistOwners = new List<PlaylistOwner>();
            return playlist;
        });

    // Act
    var (playlist, error) = await _service.CreatePlaylistAsync(request, userId, "Admin");

    // Assert
    Assert.NotNull(playlist);
    Assert.Null(error);
    Assert.True(playlist.IsAlbum);
}

[Fact]
public async Task CreatePlaylistAsync_AuthorCanCreateAlbum_CreatesSuccessfully()
{
    // Arrange
    var userId = Guid.NewGuid();
    var request = new CreatePlaylistRequest
    {
        Name = "Author Album",
        IsAlbum = true
    };

    Playlist capturedPlaylist = null!;
    _playlistRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Playlist>()))
        .Callback<Playlist>(p => capturedPlaylist = p)
        .ReturnsAsync((Playlist p) => p);

    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), true, false))
        .ReturnsAsync((Guid id, bool includeOwners, bool includeSongs) =>
        {
            var playlist = capturedPlaylist;
            playlist.PlaylistOwners = new List<PlaylistOwner>();
            return playlist;
        });

    // Act
    var (playlist, error) = await _service.CreatePlaylistAsync(request, userId, "Author");

    // Assert
    Assert.NotNull(playlist);
    Assert.Null(error);
    Assert.True(playlist.IsAlbum);
}

[Fact]
public async Task CreatePlaylistAsync_WithDescriptionAndPicture_CreatesWithAllFields()
{
    // Arrange
    var userId = Guid.NewGuid();
    var request = new CreatePlaylistRequest
    {
        Name = "Full Playlist",
        Description = "Complete description",
        Picture = "image.jpg",
        IsPublic = true,
        IsAlbum = false
    };

    Playlist capturedPlaylist = null!;
    _playlistRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Playlist>()))
        .Callback<Playlist>(p => capturedPlaylist = p)
        .ReturnsAsync((Playlist p) => p);

    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), true, false))
        .ReturnsAsync((Guid id, bool includeOwners, bool includeSongs) =>
        {
            var playlist = capturedPlaylist;
            playlist.PlaylistOwners = new List<PlaylistOwner>();
            return playlist;
        });

    // Act
    var (playlist, error) = await _service.CreatePlaylistAsync(request, userId, "User");

    // Assert
    Assert.NotNull(playlist);
    Assert.Null(error);
    Assert.Equal("Full Playlist", playlist.Name);
    Assert.Equal("Complete description", playlist.Description);
    Assert.Equal("image.jpg", playlist.Picture);
}

[Fact]
public async Task UpdatePlaylistAsync_WithDescription_UpdatesDescription()
{
    // Arrange
    var userId = Guid.NewGuid();
    var user = CreateTestUser(userId, "Owner");
    var playlist = CreateTestPlaylist("Test", isPublic: true, description: "Old Description");
    var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };
    playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, false))
        .ReturnsAsync(playlist);

    Playlist updatedPlaylist = null!;
    _playlistRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Playlist>()))
        .Callback<Playlist>(p => updatedPlaylist = p)
        .Returns(Task.CompletedTask);

    var request = new UpdatePlaylistRequest { Description = "New Description" };

    // Act
    var (success, error) = await _service.UpdatePlaylistAsync(playlist.Id, request, userId);

    // Assert
    Assert.True(success);
    Assert.Null(error);
    Assert.Equal("New Description", updatedPlaylist.Description);
}

[Fact]
public async Task UpdatePlaylistAsync_WithPicture_UpdatesPicture()
{
    // Arrange
    var userId = Guid.NewGuid();
    var user = CreateTestUser(userId, "Owner");
    var playlist = CreateTestPlaylist("Test", isPublic: true);
    var playlistOwner = new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, User = user };
    playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };

    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, true, false))
        .ReturnsAsync(playlist);

    Playlist updatedPlaylist = null!;
    _playlistRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Playlist>()))
        .Callback<Playlist>(p => updatedPlaylist = p)
        .Returns(Task.CompletedTask);

    var request = new UpdatePlaylistRequest { Picture = "newimage.jpg" };

    // Act
    var (success, error) = await _service.UpdatePlaylistAsync(playlist.Id, request, userId);

    // Assert
    Assert.True(success);
    Assert.Null(error);
    Assert.Equal("newimage.jpg", updatedPlaylist.Picture);
}

#endregion

#region Exception Tests

[Fact]
public async Task GetAllPlaylistsAsync_RepositoryThrowsException_ThrowsException()
{
    // Arrange
    _playlistRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<bool>()))
        .ThrowsAsync(new Exception("Database connection failed"));

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(() => _service.GetAllPlaylistsAsync(false));
}

[Fact]
public async Task GetPlaylistByIdAsync_RepositoryThrowsException_ThrowsException()
{
    // Arrange
    var playlistId = Guid.NewGuid();
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
        .ThrowsAsync(new InvalidOperationException("Query failed"));

    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => _service.GetPlaylistByIdAsync(playlistId));
}

[Fact]
public async Task CreatePlaylistAsync_UserRepositoryThrowsException_ThrowsException()
{
    // Arrange
    var request = new CreatePlaylistRequest
    {
        Name = "Test",
        OwnerIds = new List<Guid> { Guid.NewGuid() }
    };
    
    _userRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>()))
        .ThrowsAsync(new Exception("Database timeout"));

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(
        () => _service.CreatePlaylistAsync(request, Guid.NewGuid(), "Admin"));
}

[Fact]
public async Task CreatePlaylistAsync_PlaylistRepositoryCreateThrowsException_ThrowsException()
{
    // Arrange
    var request = new CreatePlaylistRequest { Name = "Test" };
    
    _playlistRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Playlist>()))
        .ThrowsAsync(new Exception("Constraint violation"));

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(
        () => _service.CreatePlaylistAsync(request, Guid.NewGuid(), "Admin"));
}

[Fact]
public async Task UpdatePlaylistAsync_RepositoryThrowsException_ThrowsException()
{
    // Arrange
    var playlistId = Guid.NewGuid();
    var userId = Guid.NewGuid();
    var user = CreateTestUser(userId, "Owner");
    var request = new UpdatePlaylistRequest { Name = "Updated" };
    
    var playlist = CreateTestPlaylist("Original", isPublic: true);
    playlist.Id = playlistId;
    var playlistOwner = new PlaylistOwner { UserId = userId, PlaylistId = playlistId, User = user };
    playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, false))
        .ReturnsAsync(playlist);
    
    _playlistRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Playlist>()))
        .ThrowsAsync(new Exception("Update failed"));

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(
        () => _service.UpdatePlaylistAsync(playlistId, request, userId, false));
}

[Fact]
public async Task DeletePlaylistAsync_RepositoryThrowsException_ThrowsException()
{
    // Arrange
    var playlistId = Guid.NewGuid();
    var userId = Guid.NewGuid();
    var user = CreateTestUser(userId, "Owner");
    
    var playlist = CreateTestPlaylist("Test", isPublic: true);
    playlist.Id = playlistId;
    playlist.IsAlbum = false;
    playlist.PlaylistSongs = new List<PlaylistSong>();
    var playlistOwner = new PlaylistOwner { UserId = userId, PlaylistId = playlistId, User = user };
    playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
        .ReturnsAsync(playlist);
    
    _playlistRepositoryMock.Setup(r => r.DeleteAsync(playlistId))
        .ThrowsAsync(new Exception("Foreign key constraint"));

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(
        () => _service.DeletePlaylistAsync(playlistId, userId, false));
}

[Fact]
public async Task GetSongsInPlaylistAsync_RepositoryThrowsException_ThrowsException()
{
    // Arrange
    var playlistId = Guid.NewGuid();
    
    _playlistRepositoryMock.Setup(r => r.GetByIdWithSongDetailsAsync(playlistId))
        .ThrowsAsync(new Exception("Connection lost"));

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(
        () => _service.GetSongsInPlaylistAsync(playlistId));
}

[Fact]
public async Task AddSongToPlaylistAsync_SongRepositoryThrowsException_ThrowsException()
{
    // Arrange
    var playlistId = Guid.NewGuid();
    var songId = Guid.NewGuid();
    var userId = Guid.NewGuid();
    var user = CreateTestUser(userId, "Owner");
    
    var playlist = CreateTestPlaylist("Test", isPublic: true);
    playlist.Id = playlistId;
    playlist.IsAlbum = false;
    playlist.PlaylistSongs = new List<PlaylistSong>();
    var playlistOwner = new PlaylistOwner { UserId = userId, PlaylistId = playlistId, User = user };
    playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
        .ReturnsAsync(playlist);
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(songId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
        .ThrowsAsync(new Exception("Song fetch failed"));

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(
        () => _service.AddSongToPlaylistAsync(playlistId, songId, userId, false));
}

[Fact]
public async Task AddSongToPlaylistAsync_AddToRepositoryThrowsException_ThrowsException()
{
    // Arrange
    var playlistId = Guid.NewGuid();
    var songId = Guid.NewGuid();
    var userId = Guid.NewGuid();
    var user = CreateTestUser(userId, "Owner");
    
    var playlist = CreateTestPlaylist("Test", isPublic: true);
    playlist.Id = playlistId;
    playlist.IsAlbum = false;
    playlist.PlaylistSongs = new List<PlaylistSong>();
    var playlistOwner = new PlaylistOwner { UserId = userId, PlaylistId = playlistId, User = user };
    playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
    
    var song = new Song { Id = songId, Name = "Test Song", Duration = 180 };
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
        .ReturnsAsync(playlist);
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(songId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
        .ReturnsAsync(song);
    
    _playlistRepositoryMock.Setup(r => r.AddSongToPlaylistAsync(playlistId, songId, It.IsAny<int>()))
        .ThrowsAsync(new Exception("Duplicate key"));

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(
        () => _service.AddSongToPlaylistAsync(playlistId, songId, userId, false));
}

[Fact]
public async Task AddSongToPlaylistAsync_HubNotificationThrowsException_ThrowsException()
{
    // Arrange
    var playlistId = Guid.NewGuid();
    var songId = Guid.NewGuid();
    var userId = Guid.NewGuid();
    var user = CreateTestUser(userId, "Owner");
    
    var playlist = CreateTestPlaylist("Test", isPublic: true);
    playlist.Id = playlistId;
    playlist.IsAlbum = false;
    playlist.PlaylistSongs = new List<PlaylistSong>();
    var playlistOwner = new PlaylistOwner { UserId = userId, PlaylistId = playlistId, User = user };
    playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
    
    var song = new Song { Id = songId, Name = "Test Song", Duration = 180, IsActive = true };
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
        .ReturnsAsync(playlist);
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(songId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
        .ReturnsAsync(song);
    
    _songRepositoryMock.Setup(r => r.GetSongAuthorNamesAsync(songId))
        .ReturnsAsync(new List<string>());
    
    var clientProxyMock = new Mock<IClientProxy>();
    var hubClientsMock = new Mock<IHubClients>();
    
    hubClientsMock.Setup(c => c.Group(It.IsAny<string>()))
        .Returns(clientProxyMock.Object);
    
    _mockHub.Setup(h => h.Clients).Returns(hubClientsMock.Object);
    
    clientProxyMock.Setup(c => c.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
        .ThrowsAsync(new Exception("SignalR connection lost"));

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(
        () => _service.AddSongToPlaylistAsync(playlistId, songId, userId, false));
}

[Fact]
public async Task RemoveSongFromPlaylistAsync_RepositoryThrowsException_ThrowsException()
{
    // Arrange
    var playlistId = Guid.NewGuid();
    var songId = Guid.NewGuid();
    var userId = Guid.NewGuid();
    var user = CreateTestUser(userId, "Owner");
    
    var playlist = CreateTestPlaylist("Test", isPublic: true);
    playlist.Id = playlistId;
    playlist.IsAlbum = false;
    var playlistSong = new PlaylistSong { PlaylistId = playlistId, SongId = songId };
    playlist.PlaylistSongs = new List<PlaylistSong> { playlistSong };
    var playlistOwner = new PlaylistOwner { UserId = userId, PlaylistId = playlistId, User = user };
    playlist.PlaylistOwners = new List<PlaylistOwner> { playlistOwner };
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlistId, true, true))
        .ReturnsAsync(playlist);
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(songId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
        .ReturnsAsync(new Song { Id = songId, Duration = 180 });
    
    _playlistRepositoryMock.Setup(r => r.RemoveSongFromPlaylistAsync(playlistId, songId))
        .ThrowsAsync(new Exception("Remove operation failed"));

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(
        () => _service.RemoveSongFromPlaylistAsync(playlistId, songId, userId, false));
}

[Fact]
public async Task SearchPlaylistsAsync_RepositoryThrowsException_ThrowsException()
{
    // Arrange
    var query = "test search";
    
    _playlistRepositoryMock.Setup(r => r.SearchAsync(query))
        .ThrowsAsync(new Exception("Search service unavailable"));

    // Act & Assert
    await Assert.ThrowsAsync<Exception>(
        () => _service.SearchPlaylistsAsync(query));
}

#endregion

#region Helper Methods

private Playlist CreateTestPlaylist(string name, bool isPublic)
{
    return new Playlist
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Test description",
        Picture = "test.jpg",
        IsPublic = isPublic,
        IsAlbum = false,
        IsActive = true,
        TotalDuration = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        PlaylistSongs = new List<PlaylistSong>()
    };
}

#endregion
}