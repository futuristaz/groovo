using System.ComponentModel.DataAnnotations;

namespace Groovo.DTOs.Requests;

public class UpdateProfileRequest
{
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string? Name { get; set; }

    [MaxLength(1000, ErrorMessage = "Bio cannot exceed 1000 characters")]
    public string? Bio { get; set; }

    [MaxLength(500, ErrorMessage = "Image URL cannot exceed 500 characters")]
    public string? ImageUrl { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email address")]
    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string? Email { get; set; }
}
