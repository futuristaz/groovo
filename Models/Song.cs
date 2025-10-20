using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Groovo.Models;

[Index(nameof(Name), nameof(Album), IsUnique = true)]
public class Song
{
    [Key]
    public Guid Id { get; set; }

    public Guid Album { get; set; }
    
    [MaxLength(500)]
    public string Picture { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string AudioUrl { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime ReleaseDate { get; set; }
    
    [MaxLength(100)]
    public string Genre { get; set; } = string.Empty;
    
    public string Tags { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Store duration in seconds for database
    public int Length { get; set; }
    
    // Duration as a struct
    [NotMapped]
    public Duration Duration 
    { 
        get => new Duration(Length); 
        set => Length = value.TotalSeconds; 
    }
    
    public int Plays { get; set; }
    public int Likes { get; set; }

    // Navigation properties
    public List<SongAuthor> SongAuthors { get; set; } = new List<SongAuthor>();
    public List<PlaylistSong> PlaylistSongs { get; set; } = new List<PlaylistSong>();
}
