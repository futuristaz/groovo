using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Services;

namespace Groovo.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class SharesController : ControllerBase
{
    private readonly IShareLinkService _shareLinkService;
    private readonly ILogger<SharesController> _logger;

    public SharesController(IShareLinkService shareLinkService, ILogger<SharesController> logger)
    {
        _shareLinkService = shareLinkService;
        _logger = logger;
    }

    /// <summary>POST: /api/v1/shares</summary>
    /// <returns>201 with the created share link</returns>
    [HttpPost]
    [Authorize(Roles = "User,Author,Admin")]
    public async Task<ActionResult<CreateShareLinkResponse>> Create([FromBody] CreateShareLinkRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Unauthorized("Invalid user token.");
            }

            var userRole = User.IsInRole("Admin") ? "Admin" : User.IsInRole("Author") ? "Author" : "User";
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

            var (shareLink, errorMessage) = await _shareLinkService.CreateShareLinkAsync(
                request.ResourceType,
                request.ResourceId,
                currentUserId,
                userRole,
                baseUrl
            );

            if (shareLink == null)
            {
                if (errorMessage?.Contains("not allowed", StringComparison.OrdinalIgnoreCase) == true ||
                    errorMessage?.Contains("can only share", StringComparison.OrdinalIgnoreCase) == true ||
                    errorMessage?.Contains("unsupported role", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, errorMessage);
                }

                if (errorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return NotFound(errorMessage);
                }

                return BadRequest(errorMessage ?? "Unable to create share link.");
            }

            return CreatedAtAction(nameof(Resolve), new { token = shareLink.Token }, shareLink);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating share link for resource {ResourceType}:{ResourceId}", request.ResourceType, request.ResourceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>GET: /api/v1/shares/{token}</summary>
    /// <returns>Shared playlist or track for a valid token</returns>
    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<SharedResourceResponse>> Resolve(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("Share token is required.");
            }

            var resource = await _shareLinkService.ResolveShareLinkAsync(token);
            if (resource == null)
            {
                return NotFound("Shared resource not found or link has been revoked.");
            }

            return Ok(resource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving share link token");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>DELETE: /api/v1/shares/{id}</summary>
    /// <returns>204 if successful, 404 if not found, 403 if forbidden</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "User,Author,Admin")]
    public async Task<ActionResult> Revoke(Guid id)
    {
        try
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Unauthorized("Invalid user token.");
            }

            var userRole = User.IsInRole("Admin") ? "Admin" : User.IsInRole("Author") ? "Author" : "User";

            var (success, errorMessage) = await _shareLinkService.RevokeShareLinkAsync(id, currentUserId, userRole);
            if (!success)
            {
                if (errorMessage != null)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, errorMessage);
                }

                return NotFound($"Share link with ID {id} not found.");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking share link {ShareLinkId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
