using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Groovo.Models;

public class Playlist
{
    [Key]
    public Guid Id { get; set; }
    
    [MaxLength(500)]
    public string Picture { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; }
    
    [MaxLength(1000)]
    public string Description { get; set; }

    public bool IsActive { get; set; }
    public bool IsPublic { get; set; }
    public bool IsAlbum { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdatedAt { get; set; }

    public int TotalTime { get; set; } // Total duration in seconds

    // Navigation properties
    public List<PlaylistSong> PlaylistSongs { get; set; }
    public List<PlaylistOwner> PlaylistOwners { get; set; }
}
