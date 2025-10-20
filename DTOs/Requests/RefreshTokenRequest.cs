using System.ComponentModel.DataAnnotations;

namespace Groovo.DTOs.Requests;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}