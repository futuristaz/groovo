using Groovo.Models;

namespace Groovo.DTOs.Responses;

public record UserSummaryResponse(
    Guid Id,
    string Name,
    string ImageUrl,
    UserRole Role
) : IApiResponseValue;