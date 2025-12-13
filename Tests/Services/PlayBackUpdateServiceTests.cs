using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Groovo.DTOs;
using Groovo.Hubs;
using Groovo.Repositories;
using Groovo.Services.Hub;

namespace Groovo.Tests.Services;

public class PlaybackUpdateServiceTests
{
    private readonly Mock<IPlaylistRepository> _mockPlaylistRepo;
    private readonly Mock<ISongRepository> _mockSongRepo;
    private readonly Mock<IPlaybackStateStore<string, PlaybackState>> _mockStateStore;
    private readonly Mock<IHubContext<PlaylistHub>> _mockHubContext;
    private readonly Mock<ILogger<PlaybackUpdateService>> _mockLogger;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly Mock<IHubClients> _mockClients;
    private readonly IServiceProvider _serviceProvider;

    public PlaybackUpdateServiceTests()
    {
        _mockPlaylistRepo = new Mock<IPlaylistRepository>();
        _mockSongRepo = new Mock<ISongRepository>();
        _mockStateStore = new Mock<IPlaybackStateStore<string, PlaybackState>>();
        _mockHubContext = new Mock<IHubContext<PlaylistHub>>();
        _mockLogger = new Mock<ILogger<PlaybackUpdateService>>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockClients = new Mock<IHubClients>();

        // Setup SignalR mocks
        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

        // Setup service provider
        var services = new ServiceCollection();
        services.AddScoped(_ => _mockPlaylistRepo.Object);
        services.AddScoped(_ => _mockSongRepo.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    private PlaybackUpdateService CreateService()
    {
        return new PlaybackUpdateService(
            _serviceProvider,
            _mockLogger.Object,
            _mockStateStore.Object,
            _mockHubContext.Object
        );
    }

    #region Position Update Tests

    [Fact]
    public async Task UpdatePlaybackStates_PlayingSong_UpdatesPosition()
    {
        // Arrange
        var playlistId = Guid.NewGuid().ToString();
        var songId = Guid.NewGuid();
        var lastUpdated = DateTime.UtcNow.AddSeconds(-2); // 2 seconds ago
        
        var state = new PlaybackState
        {
            CurrentSongId = songId,
            CurrentPosition = 10,
            CurrentLength = 180,
            IsPlaying = true,
            CurrentlyListening = 5,
            LastUpdated = lastUpdated
        };

        _mockStateStore.Setup(s => s.GetAllActiveStates())
            .Returns(new[] { new KeyValuePair<string, PlaybackState>(playlistId, state) });

        PlaybackState? updatedState = null;
        _mockStateStore.Setup(s => s.TryUpdate(playlistId, It.IsAny<Func<PlaybackState, PlaybackState>>()))
            .Callback<string, Func<PlaybackState, PlaybackState>>((id, updateFunc) =>
            {
                updatedState = updateFunc(state);
            })
            .Returns(() => updatedState!);

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100); // Let it run one iteration
        cts.Cancel();
        await executeTask;

        // Assert
        Assert.NotNull(updatedState);
        Assert.True(updatedState.CurrentPosition >= 12); // Should be ~12 (10 + 2 seconds)
        Assert.True(updatedState.CurrentPosition <= 13);
        
        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "PlaybackState",
                It.Is<object[]>(args => args.Length == 1 && args[0] == updatedState),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdatePlaybackStates_NotPlaying_DoesNotUpdate()
    {
        // Arrange
        var playlistId = Guid.NewGuid().ToString();
        var state = new PlaybackState
        {
            CurrentSongId = Guid.NewGuid(),
            CurrentPosition = 10,
            CurrentLength = 180,
            IsPlaying = false, // Paused
            CurrentlyListening = 5,
            LastUpdated = DateTime.UtcNow.AddSeconds(-2)
        };

        _mockStateStore.Setup(s => s.GetAllActiveStates())
            .Returns(new[] { new KeyValuePair<string, PlaybackState>(playlistId, state) });

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await executeTask;

        // Assert
        _mockStateStore.Verify(
            s => s.TryUpdate(It.IsAny<string>(), It.IsAny<Func<PlaybackState, PlaybackState>>()),
            Times.Never
        );
    }

    [Fact]
    public async Task UpdatePlaybackStates_NoListeners_DoesNotUpdate()
    {
        // Arrange
        var playlistId = Guid.NewGuid().ToString();
        var state = new PlaybackState
        {
            CurrentSongId = Guid.NewGuid(),
            CurrentPosition = 10,
            CurrentLength = 180,
            IsPlaying = true,
            CurrentlyListening = 0, // No one listening
            LastUpdated = DateTime.UtcNow.AddSeconds(-2)
        };

        _mockStateStore.Setup(s => s.GetAllActiveStates())
            .Returns(new[] { new KeyValuePair<string, PlaybackState>(playlistId, state) });

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await executeTask;

        // Assert
        _mockStateStore.Verify(
            s => s.TryUpdate(It.IsAny<string>(), It.IsAny<Func<PlaybackState, PlaybackState>>()),
            Times.Never
        );
    }

    #endregion

    #region Auto-Advance Tests

    [Fact]
    public async Task UpdatePlaybackStates_SongFinishes_AdvancesToNextSong()
    {
        // Arrange
        var playlistId = Guid.NewGuid().ToString();
        var currentSongId = Guid.NewGuid();
        var nextSongId = Guid.NewGuid();
        var followingSongId = Guid.NewGuid();
        var nextSongLength = 200;

        var state = new PlaybackState
        {
            CurrentSongId = currentSongId,
            CurrentPosition = 175,
            CurrentLength = 180,
            NextSongId = nextSongId,
            IsPlaying = true,
            CurrentlyListening = 3,
            LastUpdated = DateTime.UtcNow.AddSeconds(-10) // Will push past song length
        };

        _mockStateStore.Setup(s => s.GetAllActiveStates())
            .Returns(new[] { new KeyValuePair<string, PlaybackState>(playlistId, state) });

        _mockSongRepo.Setup(r => r.GetSongLengthAsync(nextSongId))
            .ReturnsAsync(nextSongLength);

        _mockPlaylistRepo.Setup(r => r.GetNextSongIdAsync(Guid.Parse(playlistId), nextSongId))
            .ReturnsAsync(followingSongId);

        PlaybackState? updatedState = null;
        _mockStateStore.Setup(s => s.TryUpdate(playlistId, It.IsAny<Func<PlaybackState, PlaybackState>>()))
            .Callback<string, Func<PlaybackState, PlaybackState>>((id, updateFunc) =>
            {
                updatedState = updateFunc(state);
            })
            .Returns(() => updatedState!);

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await executeTask;

        // Assert
        Assert.NotNull(updatedState);
        Assert.Equal(nextSongId, updatedState.CurrentSongId);
        Assert.Equal(0, updatedState.CurrentPosition);
        Assert.Equal(nextSongLength, updatedState.CurrentLength);
        Assert.Equal(followingSongId, updatedState.NextSongId);
        Assert.True(updatedState.IsPlaying);

        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "PlaybackState",
                It.Is<object[]>(args => args.Length == 1),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdatePlaybackStates_LastSongFinishes_StopsPlayback()
    {
        // Arrange
        var playlistId = Guid.NewGuid().ToString();
        var currentSongId = Guid.NewGuid();

        var state = new PlaybackState
        {
            CurrentSongId = currentSongId,
            CurrentPosition = 175,
            CurrentLength = 180,
            NextSongId = null, // No next song
            IsPlaying = true,
            CurrentlyListening = 3,
            LastUpdated = DateTime.UtcNow.AddSeconds(-10)
        };

        _mockStateStore.Setup(s => s.GetAllActiveStates())
            .Returns(new[] { new KeyValuePair<string, PlaybackState>(playlistId, state) });

        PlaybackState? updatedState = null;
        _mockStateStore.Setup(s => s.TryUpdate(playlistId, It.IsAny<Func<PlaybackState, PlaybackState>>()))
            .Callback<string, Func<PlaybackState, PlaybackState>>((id, updateFunc) =>
            {
                updatedState = updateFunc(state);
            })
            .Returns(() => updatedState!);

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await executeTask;

        // Assert
        Assert.NotNull(updatedState);
        Assert.False(updatedState.IsPlaying);
        Assert.Equal(state.CurrentLength, updatedState.CurrentPosition);
        Assert.Equal(currentSongId, updatedState.CurrentSongId); // Same song

        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "PlaybackState",
                It.Is<object[]>(args => args.Length == 1),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdatePlaybackStates_NextSongInvalid_StopsPlayback()
    {
        // Arrange
        var playlistId = Guid.NewGuid().ToString();
        var currentSongId = Guid.NewGuid();
        var nextSongId = Guid.NewGuid();

        var state = new PlaybackState
        {
            CurrentSongId = currentSongId,
            CurrentPosition = 175,
            CurrentLength = 180,
            NextSongId = nextSongId,
            IsPlaying = true,
            CurrentlyListening = 3,
            LastUpdated = DateTime.UtcNow.AddSeconds(-10)
        };

        _mockStateStore.Setup(s => s.GetAllActiveStates())
            .Returns(new[] { new KeyValuePair<string, PlaybackState>(playlistId, state) });

        _mockSongRepo.Setup(r => r.GetSongLengthAsync(nextSongId))
            .ReturnsAsync(0); // Invalid song length

        PlaybackState? updatedState = null;
        _mockStateStore.Setup(s => s.TryUpdate(playlistId, It.IsAny<Func<PlaybackState, PlaybackState>>()))
            .Callback<string, Func<PlaybackState, PlaybackState>>((id, updateFunc) =>
            {
                updatedState = updateFunc(state);
            })
            .Returns(() => updatedState!);

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await executeTask;

        // Assert
        Assert.NotNull(updatedState);
        Assert.False(updatedState.IsPlaying);
        Assert.Equal(state.CurrentLength, updatedState.CurrentPosition);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task UpdatePlaybackStates_RepositoryThrows_ContinuesProcessing()
    {
        // Arrange
        var playlistId1 = Guid.NewGuid().ToString();
        var playlistId2 = Guid.NewGuid().ToString();
        
        var state1 = new PlaybackState
        {
            CurrentSongId = Guid.NewGuid(),
            CurrentPosition = 175,
            CurrentLength = 180,
            NextSongId = Guid.NewGuid(),
            IsPlaying = true,
            CurrentlyListening = 1,
            LastUpdated = DateTime.UtcNow.AddSeconds(-10)
        };

        var state2 = new PlaybackState
        {
            CurrentSongId = Guid.NewGuid(),
            CurrentPosition = 10,
            CurrentLength = 180,
            IsPlaying = true,
            CurrentlyListening = 1,
            LastUpdated = DateTime.UtcNow.AddSeconds(-2)
        };

        _mockStateStore.Setup(s => s.GetAllActiveStates())
            .Returns(new[] { new KeyValuePair<string, PlaybackState>(playlistId1, state1), new KeyValuePair<string, PlaybackState>(playlistId2, state2) });

        // First playlist fails
        _mockSongRepo.Setup(r => r.GetSongLengthAsync(state1.NextSongId!.Value))
            .ThrowsAsync(new Exception("Database error"));

        // Second playlist succeeds
        PlaybackState? updatedState2 = null;
        _mockStateStore.Setup(s => s.TryUpdate(playlistId2, It.IsAny<Func<PlaybackState, PlaybackState>>()))
            .Callback<string, Func<PlaybackState, PlaybackState>>((id, updateFunc) =>
            {
                updatedState2 = updateFunc(state2);
            })
            .Returns(() => updatedState2!);

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await executeTask;

        // Assert - Second playlist still processed despite first failing
        Assert.NotNull(updatedState2);
        
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(playlistId1)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task UpdatePlaybackStates_NullCurrentSongId_SkipsUpdate()
    {
        // Arrange
        var playlistId = Guid.NewGuid().ToString();
        var state = new PlaybackState
        {
            CurrentSongId = null, // No current song
            CurrentPosition = 0,
            CurrentLength = 0,
            IsPlaying = true,
            CurrentlyListening = 5,
            LastUpdated = DateTime.UtcNow
        };

        _mockStateStore.Setup(s => s.GetAllActiveStates())
            .Returns(new[] { new KeyValuePair<string, PlaybackState>(playlistId, state) });

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await executeTask;

        // Assert
        _mockStateStore.Verify(
            s => s.TryUpdate(It.IsAny<string>(), It.IsAny<Func<PlaybackState, PlaybackState>>()),
            Times.Never
        );
    }

    [Fact]
    public async Task UpdatePlaybackStates_MultipleActivePlaylists_UpdatesAll()
    {
        // Arrange
        var playlistId1 = Guid.NewGuid().ToString();
        var playlistId2 = Guid.NewGuid().ToString();
        
        var state1 = new PlaybackState
        {
            CurrentSongId = Guid.NewGuid(),
            CurrentPosition = 10,
            CurrentLength = 180,
            IsPlaying = true,
            CurrentlyListening = 2,
            LastUpdated = DateTime.UtcNow.AddSeconds(-2)
        };

        var state2 = new PlaybackState
        {
            CurrentSongId = Guid.NewGuid(),
            CurrentPosition = 20,
            CurrentLength = 200,
            IsPlaying = true,
            CurrentlyListening = 3,
            LastUpdated = DateTime.UtcNow.AddSeconds(-1)
        };

        _mockStateStore.Setup(s => s.GetAllActiveStates())
            .Returns(new[] { new KeyValuePair<string, PlaybackState>(playlistId1, state1), new KeyValuePair<string, PlaybackState>(playlistId2, state2) });

        var updatedStates = new List<PlaybackState>();
        _mockStateStore.Setup(s => s.TryUpdate(It.IsAny<string>(), It.IsAny<Func<PlaybackState, PlaybackState>>()))
            .Callback<string, Func<PlaybackState, PlaybackState>>((id, updateFunc) =>
            {
                var original = id == playlistId1 ? state1 : state2;
                updatedStates.Add(updateFunc(original));
            })
            .Returns<string, Func<PlaybackState, PlaybackState>>((id, updateFunc) =>
            {
                var original = id == playlistId1 ? state1 : state2;
                return updateFunc(original);
            });

        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await executeTask;

        // Assert
        Assert.Equal(2, updatedStates.Count);
        Assert.All(updatedStates, state => Assert.True(state.CurrentPosition > 0));
    }

    #endregion
}