using Groovo.Services.Hub;
using Groovo.DTOs;

namespace Groovo.Tests.Services.Hub;

public class PlaybackStateStoreTests
{
    private readonly PlaybackStateStore _store;

    public PlaybackStateStoreTests()
    {
        _store = new PlaybackStateStore();
    }

    [Fact]
    public void GetOrCreate_CreatesNewState_WhenPlaylistDoesNotExist()
    {
        var playlistId = "playlist-1";

        var result = _store.GetOrCreate(playlistId);

        Assert.NotNull(result);
        Assert.Equal(0, result.CurrentlyListening);
    }

    [Fact]
    public void GetOrCreate_ReturnsSameState_WhenPlaylistExists()
    {
        var playlistId = "playlist-1";
        var firstState = _store.GetOrCreate(playlistId);
        firstState.CurrentlyListening = 5;

        var secondState = _store.GetOrCreate(playlistId);

        Assert.Same(firstState, secondState);
        Assert.Equal(5, secondState.CurrentlyListening);
    }

    [Fact]
    public void GetOrCreate_MaintainsSeparateStates_ForDifferentPlaylists()
    {
        var playlistId1 = "playlist-1";
        var playlistId2 = "playlist-2";

        var state1 = _store.GetOrCreate(playlistId1);
        var state2 = _store.GetOrCreate(playlistId2);
        state1.CurrentlyListening = 3;
        state2.CurrentlyListening = 7;

        Assert.NotSame(state1, state2);
        Assert.Equal(3, state1.CurrentlyListening);
        Assert.Equal(7, state2.CurrentlyListening);
    }

    [Fact]
    public void TryGet_ReturnsTrue_WhenPlaylistExists()
    {
        var playlistId = "playlist-1";
        var created = _store.GetOrCreate(playlistId);
        created.CurrentlyListening = 10;

        var found = _store.TryGet(playlistId, out var state);

        Assert.True(found);
        Assert.NotNull(state);
        Assert.Equal(10, state!.CurrentlyListening);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenPlaylistDoesNotExist()
    {
        var playlistId = "non-existent";

        var found = _store.TryGet(playlistId, out var state);

        Assert.False(found);
        Assert.Null(state);
    }

    [Fact]
    public void TryUpdate_CreatesAndUpdatesState_WhenPlaylistDoesNotExist()
    {
        var playlistId = "playlist-1";

        var result = _store.TryUpdate(playlistId, state =>
        {
            state.CurrentlyListening = 5;
            return state;
        });

        Assert.NotNull(result);
        Assert.Equal(5, result.CurrentlyListening);
    }

    [Fact]
    public void TryUpdate_UpdatesExistingState_WhenPlaylistExists()
    {
        var playlistId = "playlist-1";
        _store.GetOrCreate(playlistId).CurrentlyListening = 3;

        var result = _store.TryUpdate(playlistId, state =>
        {
            state.CurrentlyListening = 10;
            return state;
        });

        Assert.Equal(10, result.CurrentlyListening);
        
        _store.TryGet(playlistId, out var persistedState);
        Assert.Equal(10, persistedState!.CurrentlyListening);
    }

    [Fact]
    public void TryUpdate_AllowsIncrementalUpdates()
    {
        var playlistId = "playlist-1";
        var state = _store.GetOrCreate(playlistId);
        state.CurrentlyListening = 5;

        var result = _store.TryUpdate(playlistId, s =>
        {
            s.CurrentlyListening += 3;
            return s;
        });

        Assert.Equal(8, result.CurrentlyListening);
    }

    [Fact]
    public void TryRemove_RemovesState_WhenNoListeners()
    {
        var playlistId = "playlist-1";
        var state = _store.GetOrCreate(playlistId);
        state.CurrentlyListening = 0;

        _store.TryRemove(playlistId);

        var found = _store.TryGet(playlistId, out _);
        Assert.False(found);
    }

    [Fact]
    public void TryRemove_DoesNotRemoveState_WhenListenersExist()
    {
        var playlistId = "playlist-1";
        var state = _store.GetOrCreate(playlistId);
        state.CurrentlyListening = 5;

        _store.TryRemove(playlistId);

        var found = _store.TryGet(playlistId, out var retrievedState);
        Assert.True(found);
        Assert.Equal(5, retrievedState!.CurrentlyListening);
    }

    [Fact]
    public void TryRemove_DoesNotThrow_WhenPlaylistDoesNotExist()
    {
        var playlistId = "non-existent";

        var exception = Record.Exception(() => _store.TryRemove(playlistId));

        Assert.Null(exception);
    }

    [Fact]
    public async Task IncrementUsers_CreatesStateWithOneListener_WhenPlaylistDoesNotExist()
    {
        var playlistId = "playlist-1";

        await _store.IncrementUsers(playlistId);

        _store.TryGet(playlistId, out var state);
        Assert.Equal(1, state!.CurrentlyListening);
    }

    [Fact]
    public async Task IncrementUsers_IncrementsCount_WhenPlaylistExists()
    {
        var playlistId = "playlist-1";
        var state = _store.GetOrCreate(playlistId);
        state.CurrentlyListening = 5;

        await _store.IncrementUsers(playlistId);

        _store.TryGet(playlistId, out var updatedState);
        Assert.Equal(6, updatedState!.CurrentlyListening);
    }

    [Fact]
    public async Task IncrementUsers_IncrementsCorrectly_WhenCalledMultipleTimes()
    {
        var playlistId = "playlist-1";

        await _store.IncrementUsers(playlistId);
        await _store.IncrementUsers(playlistId);
        await _store.IncrementUsers(playlistId);

        _store.TryGet(playlistId, out var state);
        Assert.Equal(3, state!.CurrentlyListening);
    }

    [Fact]
    public async Task IncrementUsers_HandlesThreadSafety_WithConcurrentCalls()
    {
        var playlistId = "playlist-1";
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            tasks.Add(_store.IncrementUsers(playlistId));
        }
        await Task.WhenAll(tasks);

        _store.TryGet(playlistId, out var state);
        Assert.Equal(100, state!.CurrentlyListening);
    }

    [Fact]
    public async Task DecrementUsers_DecrementsCount_WhenPlaylistExists()
    {
        var playlistId = "playlist-1";
        var state = _store.GetOrCreate(playlistId);
        state.CurrentlyListening = 5;

        await _store.DecrementUsers(playlistId);

        _store.TryGet(playlistId, out var updatedState);
        Assert.Equal(4, updatedState!.CurrentlyListening);
    }

    [Fact]
    public async Task DecrementUsers_DoesNotGoNegative_WhenCountIsZero()
    {
        var playlistId = "playlist-1";
        var state = _store.GetOrCreate(playlistId);
        state.CurrentlyListening = 0;

        await _store.DecrementUsers(playlistId);

        _store.TryGet(playlistId, out var updatedState);
        Assert.Equal(0, updatedState!.CurrentlyListening);
    }

    [Fact]
    public async Task DecrementUsers_CreatesStateWithZero_WhenPlaylistDoesNotExist()
    {
        var playlistId = "playlist-1";

        await _store.DecrementUsers(playlistId);

        _store.TryGet(playlistId, out var state);
        Assert.Equal(0, state!.CurrentlyListening);
    }

    [Fact]
    public async Task DecrementUsers_HandlesThreadSafety_WithConcurrentCalls()
    {
        var playlistId = "playlist-1";
        var state = _store.GetOrCreate(playlistId);
        state.CurrentlyListening = 100;
        var tasks = new List<Task>();

        for (int i = 0; i < 50; i++)
        {
            tasks.Add(_store.DecrementUsers(playlistId));
        }
        await Task.WhenAll(tasks);

        _store.TryGet(playlistId, out var updatedState);
        Assert.Equal(50, updatedState!.CurrentlyListening);
    }

    [Fact]
    public void GetAllActiveStates_ReturnsEmpty_WhenNoStatesExist()
    {
        var result = _store.GetAllActiveStates();

        Assert.Empty(result);
    }

    [Fact]
    public void GetAllActiveStates_ReturnsEmpty_WhenAllStatesInactive()
    {
        _store.GetOrCreate("playlist-1").CurrentlyListening = 0;
        _store.GetOrCreate("playlist-2").CurrentlyListening = 0;

        var result = _store.GetAllActiveStates();

        Assert.Empty(result);
    }

    [Fact]
    public void GetAllActiveStates_ReturnsOnlyActiveStates()
    {
        _store.GetOrCreate("playlist-1").CurrentlyListening = 5;
        _store.GetOrCreate("playlist-2").CurrentlyListening = 0;
        _store.GetOrCreate("playlist-3").CurrentlyListening = 3;

        var result = _store.GetAllActiveStates().ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, kvp => kvp.Key == "playlist-1" && kvp.Value.CurrentlyListening == 5);
        Assert.Contains(result, kvp => kvp.Key == "playlist-3" && kvp.Value.CurrentlyListening == 3);
        Assert.DoesNotContain(result, kvp => kvp.Key == "playlist-2");
    }

    [Fact]
    public async Task FullWorkflow_UserJoinsAndLeaves_WorksCorrectly()
    {
        var playlistId = "playlist-1";

        await _store.IncrementUsers(playlistId);
        _store.TryGet(playlistId, out var state1);
        Assert.Equal(1, state1!.CurrentlyListening);

        await _store.IncrementUsers(playlistId);
        _store.TryGet(playlistId, out var state2);
        Assert.Equal(2, state2!.CurrentlyListening);

        await _store.DecrementUsers(playlistId);
        _store.TryGet(playlistId, out var state3);
        Assert.Equal(1, state3!.CurrentlyListening);

        await _store.DecrementUsers(playlistId);
        _store.TryGet(playlistId, out var state4);
        Assert.Equal(0, state4!.CurrentlyListening);

        _store.TryRemove(playlistId);
        var found = _store.TryGet(playlistId, out _);
        Assert.False(found);
    }

    [Fact]
    public async Task ConcurrentOperations_MaintainsConsistency_AcrossMultiplePlaylists()
    {
        var playlists = Enumerable.Range(1, 10).Select(i => $"playlist-{i}").ToList();
        var tasks = new List<Task>();

        foreach (var playlistId in playlists)
        {
            tasks.Add(Task.Run(async () =>
            {
                await _store.IncrementUsers(playlistId);
                await _store.IncrementUsers(playlistId);
                await _store.IncrementUsers(playlistId);
                await _store.DecrementUsers(playlistId);
            }));
        }

        await Task.WhenAll(tasks);

        foreach (var playlistId in playlists)
        {
            _store.TryGet(playlistId, out var state);
            Assert.Equal(2, state!.CurrentlyListening);
        }
    }

    [Fact]
    public void TryUpdate_ReflectsChanges_InGetAllActiveStates()
    {
        _store.GetOrCreate("playlist-1").CurrentlyListening = 1;
        _store.GetOrCreate("playlist-2").CurrentlyListening = 0;

        _store.TryUpdate("playlist-2", state =>
        {
            state.CurrentlyListening = 5;
            return state;
        });

        var activeStates = _store.GetAllActiveStates().ToList();

        Assert.Equal(2, activeStates.Count);
        Assert.Contains(activeStates, kvp => kvp.Key == "playlist-2" && kvp.Value.CurrentlyListening == 5);
    }
}