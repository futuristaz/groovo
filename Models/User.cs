using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Groovo.Models;

[Index(nameof(Email), IsUnique = true)]
public class User
{
    [Key]
    public Guid Id { get; set; }

    public UserRole Role { get; set; } = UserRole.User;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Bio { get; set; } = string.Empty;

    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public List<SongAuthor> SongAuthors { get; set; } = new List<SongAuthor>();
    public List<PlaylistOwner> PlaylistOwners { get; set; } = new List<PlaylistOwner>();
    public List<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}