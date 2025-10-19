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
    
    [MaxLength(500)]
    public string Picture { get; set; } = string.Empty;
    
    public List<Guid> AuthorIds { get; set; } = new();
    
    [Required]
    public Guid Album { get; set; }
    
    [MaxLength(100)]
    public string Genre { get; set; } = string.Empty;
    
    public List<string> Tags { get; set; } = new();
    
    [Required]
    [MaxLength(500)]
    public string AudioUrl { get; set; } = string.Empty;
    
    [Range(1, int.MaxValue, ErrorMessage = "Length must be greater than 0")]
    public int Length { get; set; }
}