using System.ComponentModel.DataAnnotations;

namespace Groovo.Models;

public class Notification
{
    [Key]
    public Guid Id { get; set; }

    public Guid RecipientId { get; set; }
    public User Recipient { get; set; } = null!;

    public Guid ActorId { get; set; }
    public User Actor { get; set; } = null!;

    public NotificationType Type { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum NotificationType
{
    NewFollower = 0,
    NewFriend = 1
}