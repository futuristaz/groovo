using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Groovo.Models;

[Index(nameof(PlaylistId), nameof(SongId), IsUnique = true)]
[PrimaryKey(nameof(PlaylistId), nameof(SongId))]
public class PlaylistSong
{
    public Guid PlaylistId { get; set; }
    public Guid SongId { get; set; }

    public int Order { get; set; } // For ordering songs in playlist

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime AddedAt { get; set; }
    
    // Navigation properties
    public Playlist Playlist { get; set; } = default!;
    public Song Song { get; set; } = default!;
}