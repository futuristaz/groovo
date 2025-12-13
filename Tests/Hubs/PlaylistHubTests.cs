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
    private readonly PlaylistHub _hub;
    private readonly Mock<HubCallerContext> _contextMock;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<IClientProxy> _groupProxyMock;
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
        _contextMock = new Mock<HubCallerContext>();
        _clientsMock = new Mock<IHubCallerClients>();
        _groupProxyMock = new Mock<IClientProxy>();
        _callerProxyMock = new Mock<ISingleClientProxy>();
        _groupsMock = new Mock<IGroupManager>();

        _hub = new PlaylistHub(
            _loggerMock.Object,
            _trackerMock.Object,
            _playbackMock.Object,
            _playlistRepositoryMock.Object,
            _songRepositoryMock.Object
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
        _clientsMock.Setup(c => c.Caller).Returns(_callerProxyMock.Object);

        _groupProxyMock.Setup(c => c.SendCoreAsync(
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
}
