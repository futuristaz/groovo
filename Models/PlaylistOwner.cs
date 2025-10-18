using System.ComponentModel.DataAnnotations;

namespace Groovo.Models;

[Index(nameof(PlaylistId), nameof(UserId), IsUnique = true)]
public class PlaylistOwner
{
    public Guid PlaylistId { get; set; }
    public Guid UserId { get; set; }

    // Navigation properties   
    public Playlist Playlist { get; set; }
    public User User { get; set; }
}