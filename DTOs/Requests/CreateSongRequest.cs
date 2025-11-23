using System.ComponentModel.DataAnnotations;

namespace Groovo.DTOs.Requests;

public class CreateSongRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime ReleaseDate { get; set; }
    
    public List<Guid> AuthorIds { get; set; } = new();
    
    [Required]
    public Guid Album { get; set; }
    
    [MaxLength(100)]
    public string Genre { get; set; } = string.Empty;
    
    public List<string> Tags { get; set; } = new();

    [Required]
    public string AudioId { get; set; } = string.Empty;

    [Required]
    public string ImageId { get; set; } = string.Empty;
}