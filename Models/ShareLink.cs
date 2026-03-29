using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Groovo.DTOs;

namespace Groovo.Models;

[Index(nameof(Token), IsUnique = true)]
[Index(nameof(ResourceType), nameof(ResourceId))]
public class ShareLink
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Token { get; set; } = string.Empty;

    [Required]
    public ShareResourceType ResourceType { get; set; }

    public Guid ResourceId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public User CreatedByUser { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public bool IsRevoked { get; set; }
}
