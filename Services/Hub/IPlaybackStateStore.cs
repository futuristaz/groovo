namespace Groovo.Services.Hub;

public interface IPlaybackStateStore<T1, T2>
{
    T2 GetOrCreate(T1 playlistId);
    bool TryGet(T1 playlistId, out T2? state);
    T2 TryUpdate(T1 playlistId, Func<T2, T2> updateFunc);
    void TryRemove(T1 playlistId);
    Task IncrementUsers(T1 playlistId);
    Task DecrementUsers(T1 playlistId);
    IEnumerable<KeyValuePair<T1, T2>> GetAllActiveStates();
}