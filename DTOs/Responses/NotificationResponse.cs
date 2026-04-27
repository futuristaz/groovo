namespace Groovo.DTOs.Responses;

public record NotificationResponse
{
    public Guid Id { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string ActorImageUrl { get; init; } = string.Empty;
    public Guid ActorId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool IsRead { get; init; }
    public DateTime CreatedAt { get; init; }
}