using Groovo.Models;

namespace Groovo.DTOs.Responses;

public record UserResponse(
    Guid Id,
    string Name,
    string Bio,
    string ImageUrl,
    UserRole Role,
    DateTime CreatedAt,
    DateTime UpdatedAt
) : IApiResponseValue;