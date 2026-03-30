using System.Security.Cryptography;
using Groovo.DTOs;
using Groovo.DTOs.Responses;
using Groovo.Models;
using Groovo.Repositories;

namespace Groovo.Services;

public class ShareLinkService : IShareLinkService
{
    private readonly ILogger<ShareLinkService> _logger;
    private readonly IShareLinkRepository _shareLinkRepository;
    private readonly ISongRepository _songRepository;
    private readonly IPlaylistRepository _playlistRepository;

    public ShareLinkService(
        ILogger<ShareLinkService> logger,
        IShareLinkRepository shareLinkRepository,
        ISongRepository songRepository,
        IPlaylistRepository playlistRepository)
    {
        _logger = logger;
        _shareLinkRepository = shareLinkRepository;
        _songRepository = songRepository;
        _playlistRepository = playlistRepository;
    }

    public async Task<(CreateShareLinkResponse? ShareLink, string? ErrorMessage)> CreateShareLinkAsync(
        ShareResourceType resourceType,
        Guid resourceId,
        Guid currentUserId,
        string role,
        string baseUrl)
    {
        try
        {
            var canShare = await CanUserShareResourceAsync(resourceType, resourceId, currentUserId, role);
            if (!canShare.Allowed)
            {
                return (null, canShare.ErrorMessage ?? "User cannot share this resource.");
            }

            var token = await GenerateUniqueTokenAsync();

            var link = new ShareLink
            {
                Id = Guid.NewGuid(),
                Token = token,
                ResourceType = resourceType,
                ResourceId = resourceId,
                CreatedByUserId = currentUserId,
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            };

            await _shareLinkRepository.CreateAsync(link);

            var response = new CreateShareLinkResponse(
                link.Id,
                link.Token,
                $"{baseUrl}/api/v1/shares/{link.Token}",
                link.ResourceType,
                link.ResourceId,
                link.CreatedAt
            );

            return (response, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating share link for {ResourceType} {ResourceId}", resourceType, resourceId);
            throw;
        }
    }

    public async Task<SharedResourceResponse?> ResolveShareLinkAsync(string token)
    {
        try
        {
            var link = await _shareLinkRepository.GetByTokenAsync(token);
            if (link == null || link.IsRevoked)
            {
                return null;
            }

            if (link.ResourceType == ShareResourceType.Track)
            {
                var song = await _songRepository.GetByIdAsync(link.ResourceId, includeAuthors: true);
                if (song == null)
                {
                    return null;
                }

                var authors = song.SongAuthors
                    .Where(sa => sa.User.Role == UserRole.Author)
                    .Select(sa => new AuthorResponse(
                        sa.User.Id,
                        sa.User.Name,
                        sa.User.Bio,
                        sa.User.ImageUrl
                    ))
                    .ToList();

                return new SharedResourceResponse(
                    ShareResourceType.Track,
                    new SongResponse(song, authors),
                    null
                );
            }

            var playlist = await _playlistRepository.GetByIdAsync(link.ResourceId, includeOwners: true, includeSongs: true);
            if (playlist == null)
            {
                return null;
            }

            var playlistResponse = new PlaylistResponse(
                playlist.Id,
                playlist.Name,
                playlist.Description ?? string.Empty,
                playlist.Picture ?? string.Empty,
                playlist.IsActive,
                playlist.IsPublic,
                playlist.IsAlbum,
                playlist.CreatedAt,
                playlist.UpdatedAt,
                playlist.TotalTime,
                playlist.PlaylistSongs?.Count ?? 0,
                playlist.PlaylistOwners?.Select(po => new UserSummaryResponse(
                    po.User.Id,
                    po.User.Name,
                    po.User.ImageUrl ?? string.Empty,
                    po.User.Role
                )).ToList() ?? new List<UserSummaryResponse>()
            );

            return new SharedResourceResponse(
                ShareResourceType.Playlist,
                null,
                playlistResponse
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving share link token");
            throw;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> RevokeShareLinkAsync(Guid shareLinkId, Guid currentUserId, string role)
    {
        try
        {
            var link = await _shareLinkRepository.GetByIdAsync(shareLinkId);
            if (link == null)
            {
                return (false, null);
            }

            if (role != "Admin" && link.CreatedByUserId != currentUserId)
            {
                return (false, "You are not allowed to revoke this share link.");
            }

            if (!link.IsRevoked)
            {
                await _shareLinkRepository.RevokeAsync(shareLinkId);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking share link {ShareLinkId}", shareLinkId);
            throw;
        }
    }

    private async Task<(bool Allowed, string? ErrorMessage)> CanUserShareResourceAsync(
        ShareResourceType resourceType,
        Guid resourceId,
        Guid currentUserId,
        string role)
    {
        if (resourceType == ShareResourceType.Track)
        {
            var song = await _songRepository.GetByIdAsync(resourceId, includeAuthors: true);
            if (song == null)
            {
                return (false, $"Track with ID {resourceId} not found.");
            }

            var album = await _playlistRepository.GetByIdAsync(song.Album, includeOwners: true);
            if (album == null)
            {
                return (false, $"Album with ID {song.Album} not found.");
            }

            if (role == "Admin")
            {
                return (true, null);
            }

            if (role == "User")
            {
                return album.IsPublic
                    ? (true, null)
                    : (false, "Users can only share public tracks.");
            }

            var isSongAuthor = song.SongAuthors.Any(sa => sa.UserId == currentUserId);
            var isAlbumOwner = album.PlaylistOwners.Any(po => po.UserId == currentUserId);

            if (role == "Author")
            {
                if (album.IsPublic || isSongAuthor || isAlbumOwner)
                {
                    return (true, null);
                }

                return (false, "Authors can only share tracks they own or can access.");
            }

            return (false, "Unsupported role for sharing.");
        }

        var playlist = await _playlistRepository.GetByIdAsync(resourceId, includeOwners: true);
        if (playlist == null)
        {
            return (false, $"Playlist with ID {resourceId} not found.");
        }

        if (role == "Admin")
        {
            return (true, null);
        }

        if (role == "User")
        {
            return playlist.IsPublic
                ? (true, null)
                : (false, "Users can only share public playlists.");
        }

        if (role == "Author")
        {
            if (playlist.IsPublic || playlist.PlaylistOwners.Any(po => po.UserId == currentUserId))
            {
                return (true, null);
            }

            return (false, "Authors can only share playlists they own or can access.");
        }

        return (false, "Unsupported role for sharing.");
    }

    private async Task<string> GenerateUniqueTokenAsync()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            var token = Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            var existing = await _shareLinkRepository.GetByTokenAsync(token);
            if (existing == null)
            {
                return token;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique share token.");
    }
}
