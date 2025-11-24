using System.Collections.Concurrent;

namespace Groovo.Services.Hub;

public class UserPlaylistTracker : IUserPlaylistTracker<string, string>
{
    private readonly ConcurrentDictionary<string, string> _active = new();

    public string? GetPlaylist(string connectionId) => _active.TryGetValue(connectionId, out var playlist) ? playlist : null;

    public void SetPlaylist(string connectionId, string playlist) => _active[connectionId] = playlist;

    public void Remove(string connectionId) => _active.TryRemove(connectionId, out _);
}
