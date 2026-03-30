using Groovo.DTOs;

namespace Groovo.DTOs.Responses;

public record CreateShareLinkResponse(
    Guid ShareLinkId,
    string Token,
    string Url,
    ShareResourceType ResourceType,
    Guid ResourceId,
    DateTime CreatedAt
) : IApiResponseValue;
