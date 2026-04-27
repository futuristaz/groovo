namespace Groovo.DTOs.Responses;
public record FollowUserResponse : IApiResponseValue
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
}