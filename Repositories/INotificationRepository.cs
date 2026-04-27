using Groovo.Models;

namespace Groovo.Repositories;

public interface INotificationRepository
{
    Task AddAsync(Notification notification);
    Task<List<Notification>> GetForUserAsync(Guid userId, int limit = 30);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task MarkAllReadAsync(Guid userId);
    Task MarkOneReadAsync(Guid notificationId, Guid userId);
}