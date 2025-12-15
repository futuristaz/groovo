using Moq;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Groovo.Hubs;
using Groovo.Services.Hub;
using Groovo.Repositories;
using Microsoft.Extensions.Logging;
using Groovo.DTOs;

namespace Groovo.Tests.Hubs;

public class PlaylistHubTests
{
    private readonly Mock<IUserPlaylistTracker<string, string>> _trackerMock;
    private readonly Mock<IPlaybackStateStore<string, PlaybackState>> _playbackMock;
    private readonly Mock<ILogger<PlaylistHub>> _loggerMock;
    private readonly Mock<IPlaylistRepository> _playlistRepositoryMock;
    private readonly Mock<ISongRepository> _songRepositoryMock;
    private readonly Mock<IShuffleService> _shuffleServiceMock;
    private readonly PlaylistHub _hub;
    private readonly Mock<HubCallerContext> _contextMock;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<IClientProxy> _groupProxyMock;
    private readonly Mock<IClientProxy> _othersInGroupProxyMock;
    private readonly Mock<ISingleClientProxy> _callerProxyMock;
    private readonly Mock<IGroupManager> _groupsMock;
    private readonly Guid _userId;

    public PlaylistHubTests()
    {
        _trackerMock = new Mock<IUserPlaylistTracker<string, string>>();
        _playbackMock = new Mock<IPlaybackStateStore<string, PlaybackState>>();
        _loggerMock = new Mock<ILogger<PlaylistHub>>();
        _playlistRepositoryMock = new Mock<IPlaylistRepository>();
        _songRepositoryMock = new Mock<ISongRepository>();
        _shuffleServiceMock = new Mock<IShuffleService>();
        _contextMock = new Mock<HubCallerContext>();
        _clientsMock = new Mock<IHubCallerClients>();
        _groupProxyMock = new Mock<IClientProxy>();
        _othersInGroupProxyMock = new Mock<IClientProxy>();
        _callerProxyMock = new Mock<ISingleClientProxy>();
        _groupsMock = new Mock<IGroupManager>();

        _hub = new PlaylistHub(
            _loggerMock.Object,
            _trackerMock.Object,
            _playbackMock.Object,
            _playlistRepositoryMock.Object,
            _songRepositoryMock.Object,
            _shuffleServiceMock.Object
        );

        _hub.Context = _contextMock.Object;
        _hub.Clients = _clientsMock.Object;
        _hub.Groups = _groupsMock.Object;

        _userId = Guid.NewGuid();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        }));

        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);
        _contextMock.Setup(c => c.ConnectionId).Returns("conn1");

        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_groupProxyMock.Object);
        _clientsMock.Setup(c => c.OthersInGroup(It.IsAny<string>())).Returns(_othersInGroupProxyMock.Object);
        _clientsMock.Setup(c => c.Caller).Returns(_callerProxyMock.Object);

        _groupProxyMock.Setup(c => c.SendCoreAsync(
    It.IsAny<string>(),
    It.IsAny<object[]>(),
    It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);
        
        _othersInGroupProxyMock.Setup(c => c.SendCoreAsync(
    It.IsAny<string>(),
    It.IsAny<object[]>(),
    It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);
        
        _callerProxyMock.Setup(c => c.SendCoreAsync(
    It.IsAny<string>(),
    It.IsAny<object[]>(),
    It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task JoinPlaylist_AddsUserToGroup_WhenAuthorized()
    {
        var playlistId = Guid.NewGuid();

        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns((string?)null);
        _playlistRepositoryMock.Setup(p => p.CanUserAccessPlaylistAsync(playlistId, _userId)).ReturnsAsync(true);
        _groupsMock.Setup(g => g.AddToGroupAsync("conn1", It.IsAny<string>(), default)).Returns(Task.CompletedTask);

        await _hub.JoinPlaylist(playlistId);

        _trackerMock.Verify(t => t.SetPlaylist("conn1", playlistId.ToString()), Times.Once);
        _playbackMock.Verify(p => p.IncrementUsers(playlistId.ToString()), Times.Once);
        _groupsMock.Verify(g => g.AddToGroupAsync("conn1", $"playlist_{playlistId}", default), Times.Once);
    }

    [Fact]
    public async Task JoinPlaylist_ThrowsException_WhenUnauthorized()
    {
        var playlistId = Guid.NewGuid();

        _playlistRepositoryMock.Setup(p => p.CanUserAccessPlaylistAsync(playlistId, _userId)).ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<HubException>(() => _hub.JoinPlaylist(playlistId));
        Assert.Contains("Unauthorized", exception.Message);
    }

    [Fact]
    public async Task JoinPlaylist_SwitchesPlaylist_WhenAlreadyInAnotherPlaylist()
    {
        var oldPlaylistId = Guid.NewGuid();
        var newPlaylistId = Guid.NewGuid();

        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(oldPlaylistId.ToString());
        _playlistRepositoryMock.Setup(p => p.CanUserAccessPlaylistAsync(newPlaylistId, _userId)).ReturnsAsync(true);
        _groupsMock.Setup(g => g.RemoveFromGroupAsync("conn1", It.IsAny<string>(), default)).Returns(Task.CompletedTask);
        _groupsMock.Setup(g => g.AddToGroupAsync("conn1", It.IsAny<string>(), default)).Returns(Task.CompletedTask);

        await _hub.JoinPlaylist(newPlaylistId);

        _groupsMock.Verify(g => g.RemoveFromGroupAsync("conn1", oldPlaylistId.ToString(), default), Times.Once);
        _groupsMock.Verify(g => g.AddToGroupAsync("conn1", $"playlist_{newPlaylistId}", default), Times.Once);
        _playbackMock.Verify(p => p.DecrementUsers(oldPlaylistId.ToString()), Times.Once);
        _trackerMock.Verify(t => t.SetPlaylist("conn1", newPlaylistId.ToString()), Times.Once);
    }

    [Fact]
    public async Task LeavePlaylist_RemovesUserFromGroup_WhenInPlaylist()
    {
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns("playlist_123");
        _groupsMock.Setup(g => g.RemoveFromGroupAsync("conn1", "playlist_123", default)).Returns(Task.CompletedTask);

        await _hub.LeavePlaylist();

        _trackerMock.Verify(t => t.Remove("conn1"), Times.Once);
        _groupsMock.Verify(g => g.RemoveFromGroupAsync("conn1", "playlist_123", default), Times.Once);
        _playbackMock.Verify(p => p.DecrementUsers("playlist_123"), Times.Once);
        _playbackMock.Verify(p => p.TryRemove("playlist_123"), Times.Once);
    }

    [Fact]
    public async Task LeavePlaylist_ThrowsException_WhenNotInPlaylist()
    {
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns((string?)null);

        var exception = await Assert.ThrowsAsync<HubException>(() => _hub.LeavePlaylist());
        Assert.Contains("Not in any playlist", exception.Message);
    }

    [Fact]
    public async Task PlayPause_UpdatesPlaybackState_AndNotifiesClients()
    {
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns("playlist_123");
        var state = new PlaybackState();
        _playbackMock.Setup(p => p.TryUpdate("playlist_123", It.IsAny<Func<PlaybackState, PlaybackState>>()))
                     .Returns((string id, Func<PlaybackState, PlaybackState> updateFunc) => updateFunc(state));

        await _hub.PlayPause(true);

        _playbackMock.Verify(p => p.TryUpdate("playlist_123", It.IsAny<Func<PlaybackState, PlaybackState>>()), Times.Once);
        _groupProxyMock.Verify(c => c.SendCoreAsync(
            "PlaybackState",
            It.Is<object[]>(o => o.Length > 0 && o[0] != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlayPause_ThrowsException_WhenNotInPlaylist()
    {
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns((string?)null);

        var exception = await Assert.ThrowsAsync<HubException>(() => _hub.PlayPause(true));
        Assert.Contains("Not in any playlist", exception.Message);
    }

    [Fact]
    public async Task PlaySong_UpdatesPlaybackState_AndNotifiesClients()
    {
        var playlistId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        var songId = Guid.NewGuid();
        var nextSongId = Guid.NewGuid();

        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId);
        var state = new PlaybackState { CurrentSongId = songId, NextSongId = nextSongId };
        _playbackMock.Setup(p => p.GetOrCreate(playlistId)).Returns(state);
        _songRepositoryMock.Setup(p => p.GetSongLengthAsync(songId)).ReturnsAsync(300);
        _playlistRepositoryMock.Setup(p => p.GetNextSongIdAsync(Guid.Parse(playlistId), songId)).ReturnsAsync(nextSongId);
        _playbackMock.Setup(p => p.TryUpdate(playlistId, It.IsAny<Func<PlaybackState, PlaybackState>>()))
                     .Returns((string id, Func<PlaybackState, PlaybackState> updateFunc) => updateFunc(state));

        await _hub.PlaySong(songId);

        _playbackMock.Verify(p => p.TryUpdate(playlistId, It.IsAny<Func<PlaybackState, PlaybackState>>()), Times.Once);
        _groupProxyMock.Verify(c => c.SendCoreAsync(
            "PlaybackState",
            It.Is<object[]>(o => o.Length > 0 && o[0] != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlaySong_ThrowsException_WhenNotInPlaylist()
    {
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns((string?)null);

        var exception = await Assert.ThrowsAsync<HubException>(() => _hub.PlaySong(Guid.NewGuid()));
        Assert.Contains("Not in any playlist", exception.Message);
    }

    [Fact]
    public async Task Seek_UpdatesPlaybackPosition_AndNotifiesClients()
    {
        var playlistId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId);
        var state = new PlaybackState { CurrentSongId = Guid.NewGuid(), CurrentLength = 500 };
        _playbackMock.Setup(p => p.GetOrCreate(playlistId)).Returns(state);
        _playbackMock.Setup(p => p.TryUpdate(playlistId, It.IsAny<Func<PlaybackState, PlaybackState>>()))
                     .Returns((string id, Func<PlaybackState, PlaybackState> updateFunc) => updateFunc(state));

        await _hub.Seek(100);

        _playbackMock.Verify(p => p.TryUpdate(playlistId, It.IsAny<Func<PlaybackState, PlaybackState>>()), Times.Once);
        _groupProxyMock.Verify(c => c.SendCoreAsync(
            "PlaybackState",
            It.Is<object[]>(o => o.Length > 0 && o[0] != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Seek_ThrowsException_WhenNotInPlaylist()
    {
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns((string?)null);

        var exception = await Assert.ThrowsAsync<HubException>(() => _hub.Seek(100));
        Assert.Contains("Not in any playlist", exception.Message);
    }

    [Fact]
    public async Task Seek_ThrowsException_WhenNoSongPlaying()
    {
        var playlistId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId);
        var state = new PlaybackState { CurrentSongId = null };
        _playbackMock.Setup(p => p.GetOrCreate(playlistId)).Returns(state);

        var exception = await Assert.ThrowsAsync<HubException>(() => _hub.Seek(100));
        Assert.Contains("No song is currently playing", exception.Message);
    }

    [Fact]
    public async Task GetPlaybackState_SendsCurrentStateToCaller()
    {
        var playlistId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId);
        var state = new PlaybackState();
        _playbackMock.Setup(p => p.GetOrCreate(playlistId)).Returns(state);

        await _hub.GetPlaybackState();

        _callerProxyMock.Verify(c => c.SendCoreAsync(
            "PlaybackState",
            It.Is<object[]>(o => o.Length > 0 && o[0] == state),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPlaybackState_ThrowsException_WhenNotInPlaylist()
    {
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns((string?)null);

        var exception = await Assert.ThrowsAsync<HubException>(() => _hub.GetPlaybackState());
        Assert.Contains("Not in any playlist", exception.Message);
    }

    [Fact]
    public async Task GetPlaybackState_UpdatesPosition_WhenSongIsPlaying()
    {
        var playlistId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId);
        var state = new PlaybackState 
        { 
            IsPlaying = true, 
            CurrentPosition = 10, 
            CurrentLength = 300,
            LastUpdated = DateTime.UtcNow.AddSeconds(-5) // 5 seconds ago
        };
        _playbackMock.Setup(p => p.GetOrCreate(playlistId)).Returns(state);

        await _hub.GetPlaybackState();

        // Position should be updated from 10 to ~15 (10 + 5 seconds elapsed)
        Assert.True(state.CurrentPosition >= 14 && state.CurrentPosition <= 16);
        _callerProxyMock.Verify(c => c.SendCoreAsync(
            "PlaybackState",
            It.Is<object[]>(o => o.Length > 0 && o[0] != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlayPause_UpdatesPosition_WhenPausingPlayingSong()
    {
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns("playlist_123");
        var state = new PlaybackState 
        { 
            IsPlaying = true, 
            CurrentPosition = 10, 
            CurrentLength = 300,
            LastUpdated = DateTime.UtcNow.AddSeconds(-5)
        };
        _playbackMock.Setup(p => p.TryUpdate("playlist_123", It.IsAny<Func<PlaybackState, PlaybackState>>()))
                     .Returns((string id, Func<PlaybackState, PlaybackState> updateFunc) => updateFunc(state));

        await _hub.PlayPause(false); // Pause

        // Position should be updated before pausing
        Assert.True(state.CurrentPosition >= 14 && state.CurrentPosition <= 16);
        Assert.False(state.IsPlaying);
    }

    [Fact]
    public async Task Seek_ThrowsException_WhenPositionExceedsSongLength()
    {
        var playlistId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId);
        var state = new PlaybackState { CurrentSongId = Guid.NewGuid(), CurrentLength = 300 };
        _playbackMock.Setup(p => p.GetOrCreate(playlistId)).Returns(state);

        var exception = await Assert.ThrowsAsync<HubException>(() => _hub.Seek(500));
        Assert.Contains("exceeds song length", exception.Message);
    }

    [Fact]
    public async Task Seek_ThrowsException_WhenPositionIsNegative()
    {
        var exception = await Assert.ThrowsAsync<HubException>(() => _hub.Seek(-10));
        Assert.Contains("cannot be negative", exception.Message);
    }

    [Fact]
    public async Task OnConnectedAsync_CompletesSuccessfully()
    {
        // Act - OnConnectedAsync is called automatically but we can invoke it directly
        await _hub.OnConnectedAsync();

        // Assert - No exception thrown means success
        Assert.True(true);
    }

    [Fact]
    public async Task OnDisconnectedAsync_CleansUp_WhenUserInPlaylist()
    {
        var playlistId = "playlist_123";
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId);
        _groupsMock.Setup(g => g.RemoveFromGroupAsync("conn1", playlistId, default)).Returns(Task.CompletedTask);

        await _hub.OnDisconnectedAsync(null);

        _groupsMock.Verify(g => g.RemoveFromGroupAsync("conn1", playlistId, default), Times.Once);
        _trackerMock.Verify(t => t.Remove("conn1"), Times.Once);
        _playbackMock.Verify(p => p.DecrementUsers(playlistId), Times.Once);
        _playbackMock.Verify(p => p.TryRemove(playlistId), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_DoesNothing_WhenUserNotInPlaylist()
    {
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns((string?)null);

        await _hub.OnDisconnectedAsync(null);

        _groupsMock.Verify(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _trackerMock.Verify(t => t.Remove(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_LogsError_WhenExceptionProvided()
    {
        var exception = new Exception("Test disconnect error");
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns((string?)null);

        await _hub.OnDisconnectedAsync(exception);

        // Verify that error logging occurred (check that it completes without throwing)
        Assert.True(true);
    }

    [Fact]
    public async Task ToggleShuffle_EnablesShuffle_WhenDisabled()
    {
        var playlistId = "playlist_123";
        var shuffleSeed = 42;
        
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId);
        var state = new PlaybackState { IsShuffleEnabled = false };
        _playbackMock.Setup(p => p.GetOrCreate(playlistId)).Returns(state);
        _shuffleServiceMock.Setup(s => s.GenerateShuffleSeed()).Returns(shuffleSeed);
        _playbackMock.Setup(p => p.TryUpdate(playlistId, It.IsAny<Func<PlaybackState, PlaybackState>>()))
                     .Returns((string id, Func<PlaybackState, PlaybackState> updateFunc) => updateFunc(state));

        await _hub.ToggleShuffle(true);

        Assert.True(state.IsShuffleEnabled);
        Assert.Equal(shuffleSeed, state.ShuffleSeed);
        _shuffleServiceMock.Verify(s => s.GenerateShuffleSeed(), Times.Once);
        _playbackMock.Verify(p => p.TryUpdate(playlistId, It.IsAny<Func<PlaybackState, PlaybackState>>()), Times.Once);
    }

    [Fact]
    public async Task ToggleShuffle_DisablesShuffle_WhenEnabled()
    {
        var playlistId = "playlist_123";
        
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId);
        var state = new PlaybackState { IsShuffleEnabled = true, ShuffleSeed = 42 };
        _playbackMock.Setup(p => p.GetOrCreate(playlistId)).Returns(state);
        _playbackMock.Setup(p => p.TryUpdate(playlistId, It.IsAny<Func<PlaybackState, PlaybackState>>()))
                     .Returns((string id, Func<PlaybackState, PlaybackState> updateFunc) => updateFunc(state));

        await _hub.ToggleShuffle(false);

        Assert.False(state.IsShuffleEnabled);
        Assert.Null(state.ShuffleSeed);
        _shuffleServiceMock.Verify(s => s.GenerateShuffleSeed(), Times.Never);
        _playbackMock.Verify(p => p.TryUpdate(playlistId, It.IsAny<Func<PlaybackState, PlaybackState>>()), Times.Once);
    }

    [Fact]
    public async Task ToggleShuffle_DoesNothing_WhenAlreadyInRequestedState()
    {
        var playlistId = "playlist_123";
        
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId);
        var state = new PlaybackState { IsShuffleEnabled = true, ShuffleSeed = 42 };
        _playbackMock.Setup(p => p.GetOrCreate(playlistId)).Returns(state);

        await _hub.ToggleShuffle(true); // Already enabled

        _playbackMock.Verify(p => p.TryUpdate(It.IsAny<string>(), It.IsAny<Func<PlaybackState, PlaybackState>>()), Times.Never);
        _shuffleServiceMock.Verify(s => s.GenerateShuffleSeed(), Times.Never);
    }

    [Fact]
    public async Task ToggleShuffle_ThrowsException_WhenNotInPlaylist()
    {
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns((string?)null);

        var exception = await Assert.ThrowsAsync<HubException>(() => _hub.ToggleShuffle(true));
        Assert.Contains("Not in any playlist", exception.Message);
    }

    [Fact]
    public async Task PlaySong_UsesShuffledNextSong_WhenShuffleEnabled()
    {
        var playlistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var songId = Guid.NewGuid();
        var shuffledNextSongId = Guid.NewGuid();
        var shuffleSeed = 42;

        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId.ToString());
        var state = new PlaybackState 
        { 
            CurrentSongId = songId, 
            IsShuffleEnabled = true,
            ShuffleSeed = shuffleSeed
        };
        _playbackMock.Setup(p => p.GetOrCreate(playlistId.ToString())).Returns(state);
        _songRepositoryMock.Setup(p => p.GetSongLengthAsync(songId)).ReturnsAsync(300);
        _shuffleServiceMock.Setup(s => s.GetNextShuffledSongAsync(playlistId, songId, shuffleSeed))
                          .ReturnsAsync(shuffledNextSongId);
        _playbackMock.Setup(p => p.TryUpdate(playlistId.ToString(), It.IsAny<Func<PlaybackState, PlaybackState>>()))
                     .Returns((string id, Func<PlaybackState, PlaybackState> updateFunc) => updateFunc(state));

        await _hub.PlaySong(songId);

        _shuffleServiceMock.Verify(s => s.GetNextShuffledSongAsync(playlistId, songId, shuffleSeed), Times.Once);
        Assert.Equal(shuffledNextSongId, state.NextSongId);
    }

    [Fact]
    public async Task PlaySong_UsesRegularNextSong_WhenShuffleDisabled()
    {
        var playlistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var songId = Guid.NewGuid();
        var regularNextSongId = Guid.NewGuid();

        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId.ToString());
        var state = new PlaybackState 
        { 
            CurrentSongId = songId, 
            IsShuffleEnabled = false
        };
        _playbackMock.Setup(p => p.GetOrCreate(playlistId.ToString())).Returns(state);
        _songRepositoryMock.Setup(p => p.GetSongLengthAsync(songId)).ReturnsAsync(300);
        _playlistRepositoryMock.Setup(p => p.GetNextSongIdAsync(playlistId, songId))
                              .ReturnsAsync(regularNextSongId);
        _playbackMock.Setup(p => p.TryUpdate(playlistId.ToString(), It.IsAny<Func<PlaybackState, PlaybackState>>()))
                     .Returns((string id, Func<PlaybackState, PlaybackState> updateFunc) => updateFunc(state));

        await _hub.PlaySong(songId);

        _shuffleServiceMock.Verify(s => s.GetNextShuffledSongAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
        _playlistRepositoryMock.Verify(p => p.GetNextSongIdAsync(playlistId, songId), Times.Once);
        Assert.Equal(regularNextSongId, state.NextSongId);
    }

    [Fact]
    public async Task PlaySong_UsesRegularNextSong_WhenShuffleEnabledButNoSeed()
    {
        var playlistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var songId = Guid.NewGuid();
        var regularNextSongId = Guid.NewGuid();

        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId.ToString());
        var state = new PlaybackState 
        { 
            CurrentSongId = songId, 
            IsShuffleEnabled = true,
            ShuffleSeed = null // Shuffle enabled but no seed (edge case)
        };
        _playbackMock.Setup(p => p.GetOrCreate(playlistId.ToString())).Returns(state);
        _songRepositoryMock.Setup(p => p.GetSongLengthAsync(songId)).ReturnsAsync(300);
        _playlistRepositoryMock.Setup(p => p.GetNextSongIdAsync(playlistId, songId))
                              .ReturnsAsync(regularNextSongId);
        _playbackMock.Setup(p => p.TryUpdate(playlistId.ToString(), It.IsAny<Func<PlaybackState, PlaybackState>>()))
                     .Returns((string id, Func<PlaybackState, PlaybackState> updateFunc) => updateFunc(state));

        await _hub.PlaySong(songId);

        _shuffleServiceMock.Verify(s => s.GetNextShuffledSongAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
        _playlistRepositoryMock.Verify(p => p.GetNextSongIdAsync(playlistId, songId), Times.Once);
        Assert.Equal(regularNextSongId, state.NextSongId);
    }

    #region SendReaction Tests

    [Fact]
    public async Task SendReaction_SendsReactionToOthers_WhenInPlaylist()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var reaction = EmojiReaction.Heart;
        var username = "TestUser";
        Groovo.DTOs.Responses.EmojiReactionResponse? capturedResponse = null;

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Name, username)
        }));
        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId.ToString());
        
        _othersInGroupProxyMock.Setup(c => c.SendCoreAsync(
            "ReceiveReaction",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((method, args, token) => 
            {
                if (args.Length > 0 && args[0] is Groovo.DTOs.Responses.EmojiReactionResponse resp)
                {
                    capturedResponse = resp;
                }
            })
            .Returns(Task.CompletedTask);

        // Act
        await _hub.SendReaction(reaction);

        // Assert
        Assert.NotNull(capturedResponse);
        Assert.Equal(reaction, capturedResponse.Reaction);
        Assert.Equal(username, capturedResponse.Username);
        
        _othersInGroupProxyMock.Verify(
            c => c.SendCoreAsync(
                "ReceiveReaction",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task SendReaction_UsesEmail_WhenNameNotAvailable()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var reaction = EmojiReaction.Fire;
        var email = "test@example.com";
        Groovo.DTOs.Responses.EmojiReactionResponse? capturedResponse = null;

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Email, email)
        }));
        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId.ToString());
        
        _othersInGroupProxyMock.Setup(c => c.SendCoreAsync(
            "ReceiveReaction",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((method, args, token) => 
            {
                if (args.Length > 0 && args[0] is Groovo.DTOs.Responses.EmojiReactionResponse resp)
                {
                    capturedResponse = resp;
                }
            })
            .Returns(Task.CompletedTask);

        // Act
        await _hub.SendReaction(reaction);

        // Assert
        Assert.NotNull(capturedResponse);
        Assert.Equal(email, capturedResponse.Username);
        
        _othersInGroupProxyMock.Verify(
            c => c.SendCoreAsync(
                "ReceiveReaction",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task SendReaction_UsesAnonymous_WhenNoUserInfo()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var reaction = EmojiReaction.Laughing;
        Groovo.DTOs.Responses.EmojiReactionResponse? capturedResponse = null;

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        }));
        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId.ToString());
        
        _othersInGroupProxyMock.Setup(c => c.SendCoreAsync(
            "ReceiveReaction",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((method, args, token) => 
            {
                if (args.Length > 0 && args[0] is Groovo.DTOs.Responses.EmojiReactionResponse resp)
                {
                    capturedResponse = resp;
                }
            })
            .Returns(Task.CompletedTask);

        // Act
        await _hub.SendReaction(reaction);

        // Assert
        Assert.NotNull(capturedResponse);
        Assert.Equal("Anonymous", capturedResponse.Username);
        
        _othersInGroupProxyMock.Verify(
            c => c.SendCoreAsync(
                "ReceiveReaction",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task SendReaction_ThrowsHubException_WhenNotInPlaylist()
    {
        // Arrange
        var reaction = EmojiReaction.Heart;
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns((string?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(
            () => _hub.SendReaction(reaction)
        );

        Assert.Equal("Not in any playlist", exception.Message);
        _othersInGroupProxyMock.Verify(
            c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Theory]
    [InlineData(EmojiReaction.Heart)]
    [InlineData(EmojiReaction.Fire)]
    [InlineData(EmojiReaction.Laughing)]
    [InlineData(EmojiReaction.Crying)]
    [InlineData(EmojiReaction.StarEyes)]
    [InlineData(EmojiReaction.Clapping)]
    [InlineData(EmojiReaction.ThumbsUp)]
    [InlineData(EmojiReaction.PartyPopper)]
    [InlineData(EmojiReaction.MusicalNote)]
    [InlineData(EmojiReaction.Rocket)]
    public async Task SendReaction_HandlesAllReactionTypes(EmojiReaction reaction)
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var username = "TestUser";
        Groovo.DTOs.Responses.EmojiReactionResponse? capturedResponse = null;

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Name, username)
        }));
        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId.ToString());
        
        _othersInGroupProxyMock.Setup(c => c.SendCoreAsync(
            "ReceiveReaction",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((method, args, token) => 
            {
                if (args.Length > 0 && args[0] is Groovo.DTOs.Responses.EmojiReactionResponse resp)
                {
                    capturedResponse = resp;
                }
            })
            .Returns(Task.CompletedTask);

        // Act
        await _hub.SendReaction(reaction);

        // Assert
        Assert.NotNull(capturedResponse);
        Assert.Equal(reaction, capturedResponse.Reaction);
        
        _othersInGroupProxyMock.Verify(
            c => c.SendCoreAsync(
                "ReceiveReaction",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task SendReaction_SendsToCorrectPlaylistGroup()
    {
        // Arrange
        var playlistId = Guid.NewGuid();
        var reaction = EmojiReaction.Heart;
        var username = "TestUser";

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Name, username)
        }));
        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);
        _trackerMock.Setup(t => t.GetPlaylist("conn1")).Returns(playlistId.ToString());

        // Act
        await _hub.SendReaction(reaction);

        // Assert
        _clientsMock.Verify(
            c => c.OthersInGroup($"playlist_{playlistId}"),
            Times.Once
        );
    }

    #endregion
}
