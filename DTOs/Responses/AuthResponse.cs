namespace Groovo.DTOs.Responses;

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt
) : IApiResponseValue;