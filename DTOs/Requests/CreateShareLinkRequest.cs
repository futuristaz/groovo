using System.ComponentModel.DataAnnotations;
using Groovo.DTOs;

namespace Groovo.DTOs.Requests;

public class CreateShareLinkRequest
{
    [Required]
    public ShareResourceType ResourceType { get; set; }

    [Required]
    public Guid ResourceId { get; set; }
}
