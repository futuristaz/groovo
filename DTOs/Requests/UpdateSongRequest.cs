using System.ComponentModel.DataAnnotations;

namespace Groovo.DTOs.Requests;

public class UpdateSongRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }
    
    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime? ReleaseDate { get; set; }
    
    public Guid? Album { get; set; }
    
    [MaxLength(100)]
    public string? Genre { get; set; }
    
    public List<string>? Tags { get; set; }
}