namespace Groovo.DTOs.Responses;

public record AuthorResponse(
    Guid Id,
    string Name,
    string Bio,
    string ImageUrl
);