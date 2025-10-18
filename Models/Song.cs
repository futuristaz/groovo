using System.ComponentModel.DataAnnotations;

namespace Groovo.Models;

[Index(nameof(Name), nameof(Album), IsUnique = true)]
public class Song
{
    [Key]
    public Guid Id { get; set; }

    public Guid Album { get; set; }
    
    [MaxLength(500)]
    public string Picture { get; set; }

    [Required]
    [MaxLength(500)]
    public string AudioUrl { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; }
    
    [MaxLength(1000)]
    public string Description { get; set; }

    [Required]
    public DateTime ReleaseDate { get; set; }
    
    [MaxLength(100)]
    public string Genre { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string Tags { get; set; }
    
    public bool IsActive { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdatedAt { get; set; }
    
    public int Length { get; set; } // Duration in seconds
    public int Plays { get; set; }
    public int Likes { get; set; }

    // Navigation properties
    public List<SongAuthor> SongAuthors { get; set; }
    public List<PlaylistSong> PlaylistSongs { get; set; }
}
