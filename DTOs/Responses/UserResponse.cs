namespace Groovo.DTOs.Responses;

public record UserResponse(
    Guid Id,
    string Name,
    string Bio,
    string ImageUrl,
    bool IsAuthor,
    DateTime CreatedAt,
    DateTime UpdatedAt
);