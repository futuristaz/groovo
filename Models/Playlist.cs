using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Groovo.Models;

public class Playlist
{
    [Key]
    public Guid Id { get; set; }
    
    [MaxLength(500)]
    public string Picture { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public bool IsPublic { get; set; }
    public bool IsAlbum { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdatedAt { get; set; }

    public int TotalTime { get; set; }
    
    // Total duration as a struct
    [NotMapped]
    public Duration TotalDuration 
    { 
        get => new Duration(TotalTime); 
        set => TotalTime = value.TotalSeconds; 
    }

    // Navigation properties
    public List<PlaylistSong> PlaylistSongs { get; set; } = new List<PlaylistSong>();
    public List<PlaylistOwner> PlaylistOwners { get; set; } = new List<PlaylistOwner>();
}
