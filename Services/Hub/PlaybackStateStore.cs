using System.Collections.Concurrent;
using Groovo.DTOs;

namespace Groovo.Services.Hub;

public interface IPlaybackStateStore<T1, T2>
{
    T2 GetOrCreate(T1 playlistId);
    bool TryGet(T1 playlistId, out T2? state);
    T2 TryUpdate(T1 playlistId, Func<T2, T2> updateFunc);
    void TryRemove(T1 playlistId);
    Task IncrementUsers(T1 playlistId);
    Task DecrementUsers(T1 playlistId);
}

public class PlaybackStateStore : IPlaybackStateStore<string, PlaybackState>
{
    private readonly ConcurrentDictionary<string, PlaybackState> _states = new();

    public PlaybackState GetOrCreate(string playlistId)
    {
        return _states.GetOrAdd(playlistId, _ => new PlaybackState());
    }

    public bool TryGet(string playlistId, out PlaybackState? state)
    {
        return _states.TryGetValue(playlistId, out state);
    }

    public PlaybackState TryUpdate(string playlistId, Func<PlaybackState, PlaybackState> updateFunc)
    {
        return _states.AddOrUpdate(
            playlistId,
            _ => updateFunc(new PlaybackState()),
            (_, existingState) => updateFunc(existingState)
        );
    }

    public void TryRemove(string playlistId)
    {
        // Remove if no users are listening
        if (_states.TryGetValue(playlistId, out var state) && state.CurrentlyListening == 0)
        {
            _states.TryRemove(playlistId, out _);
        }
    }

    public async Task IncrementUsers(string playlistId)
    {
        await Task.Run(() => _states.AddOrUpdate(
            playlistId,
            _ => new PlaybackState { CurrentlyListening = 1 },
            (_, existingState) =>
            {
                existingState.CurrentlyListening++;
                return existingState;
            }
        ));
    }

    public async Task DecrementUsers(string playlistId)
    {
        await Task.Run(() => _states.AddOrUpdate(
            playlistId,
            _ => new PlaybackState { CurrentlyListening = 0 },
            (_, existingState) =>
            {
                if (existingState.CurrentlyListening > 0)
                {
                    existingState.CurrentlyListening--;
                }
                return existingState;
            }
        ));
    }
}