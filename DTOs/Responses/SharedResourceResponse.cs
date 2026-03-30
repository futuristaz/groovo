using Groovo.DTOs;

namespace Groovo.DTOs.Responses;

public record SharedResourceResponse(
    ShareResourceType ResourceType,
    SongResponse? Track,
    PlaylistResponse? Playlist
) : IApiResponseValue;
