using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Groovo.Hubs
{
    public class PlaylistHub : Hub
    {
        // Called when a client adds a song to the playlist
        public async Task AddSong(string songTitle, string artist)
        {
            // Broadcast to everyone (including sender)
            await Clients.All.SendAsync("SongAdded", songTitle, artist);
        }

        // notify others when someone connects and leaves
        public override async Task OnConnectedAsync()
        {
            await Clients.All.SendAsync("UserJoined", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await Clients.All.SendAsync("UserLeft", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
