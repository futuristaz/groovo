using System.ComponentModel.DataAnnotations;

namespace Groovo.DTOs.Requests;

public class UpdatePlaylistRequest
{
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string? Name { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }

    [MaxLength(500, ErrorMessage = "Picture URL cannot exceed 500 characters")]
    public string? Picture { get; set; }

    public bool? IsPublic { get; set; }
}