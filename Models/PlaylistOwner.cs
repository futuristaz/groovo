using Microsoft.EntityFrameworkCore;

namespace Groovo.Models;

[Index(nameof(PlaylistId), nameof(UserId), IsUnique = true)]
[PrimaryKey(nameof(PlaylistId), nameof(UserId))]
public class PlaylistOwner
{
    public Guid PlaylistId { get; set; }
    public Guid UserId { get; set; }

    // Navigation properties   
    public Playlist Playlist { get; set; } = default!;
    public User User { get; set; } = default!;
}