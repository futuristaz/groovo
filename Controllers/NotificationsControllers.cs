using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Groovo.DTOs.Responses;
using Groovo.Repositories;
using System.Security.Claims;

namespace Groovo.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/notifications")]
[ApiVersion("1.0")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationRepository notificationRepository,
        ILogger<NotificationsController> logger)
    {
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>GET: /api/v1/notifications — last 30 notifications</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationResponse>>> GetAll()
    {
        try
        {
            var notifications = await _notificationRepository.GetForUserAsync(CurrentUserId);
            var response = notifications.Select(n => new NotificationResponse
            {
                Id = n.Id,
                ActorId = n.ActorId,
                ActorName = n.Actor.Name,
                ActorImageUrl = n.Actor.ImageUrl,
                Type = n.Type.ToString(),
                Message = n.Type == Models.NotificationType.NewFriend
                    ? $"{n.Actor.Name} and you are now friends!"
                    : $"{n.Actor.Name} started following you.",
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            });
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>GET: /api/v1/notifications/unread-count</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        try
        {
            var count = await _notificationRepository.GetUnreadCountAsync(CurrentUserId);
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unread count");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>PUT: /api/v1/notifications/read-all</summary>
    [HttpPut("read-all")]
    public async Task<ActionResult> MarkAllRead()
    {
        try
        {
            await _notificationRepository.MarkAllReadAsync(CurrentUserId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notifications as read");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>PUT: /api/v1/notifications/{id}/read</summary>
    [HttpPut("{id:guid}/read")]
    public async Task<ActionResult> MarkOneRead(Guid id)
    {
        try
        {
            await _notificationRepository.MarkOneReadAsync(id, CurrentUserId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read");
            return StatusCode(500, "Internal server error");
        }
    }
}