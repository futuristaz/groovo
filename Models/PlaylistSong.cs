using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Groovo.Models;

[Index(nameof(PlaylistId), nameof(SongId), IsUnique = true)]
public class PlaylistSong
{
    public Guid PlaylistId { get; set; }
    public Guid SongId { get; set; }

    public int Order { get; set; } // For ordering songs in playlist

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime AddedAt { get; set; }
    
    // Navigation properties
    public Playlist Playlist { get; set; }
    public Song Song { get; set; }
}