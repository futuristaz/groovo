using System.ComponentModel.DataAnnotations;

namespace Groovo.Models;

public class User
{
    [Key]
    public Guid Id { get; set; }

    public bool IsAuthor { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; }

    [MaxLength(1000)]
    public string Bio { get; set; }

    [MaxLength(500)]
    public string ImageUrl { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public List<SongAuthor> SongAuthors { get; set; }
    public List<PlaylistOwner> PlaylistOwners { get; set; }
}