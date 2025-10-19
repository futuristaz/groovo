using System.ComponentModel.DataAnnotations;

namespace Groovo.Models;

public class User
{
    [Key]
    public Guid Id { get; set; }

    public bool IsAuthor { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Bio { get; set; } = string.Empty;

    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public List<SongAuthor> SongAuthors { get; set; } = new List<SongAuthor>();
    public List<PlaylistOwner> PlaylistOwners { get; set; } = new List<PlaylistOwner>();
}