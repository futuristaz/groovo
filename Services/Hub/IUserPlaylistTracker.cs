namespace Groovo.Services.Hub;

public interface IUserPlaylistTracker<T1, T2>
{
    T2? GetPlaylist(T1 connectionId);
    void SetPlaylist(T1 connectionId, T2 playlist);
    void Remove(T1 connectionId);
}