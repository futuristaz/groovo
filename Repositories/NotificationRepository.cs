using Groovo.Data.Contexts;
using Groovo.Models;
using Microsoft.EntityFrameworkCore;

namespace Groovo.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _db;

    public NotificationRepository(ApplicationDbContext db) => _db = db;

    public async Task AddAsync(Notification notification)
    {
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
    }

    public Task<List<Notification>> GetForUserAsync(Guid userId, int limit = 30) =>
        _db.Notifications
            .Where(n => n.RecipientId == userId)
            .Include(n => n.Actor)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public Task<int> GetUnreadCountAsync(Guid userId) =>
        _db.Notifications
            .CountAsync(n => n.RecipientId == userId && !n.IsRead);

    public async Task MarkAllReadAsync(Guid userId)
    {
        await _db.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task MarkOneReadAsync(Guid notificationId, Guid userId)
    {
        await _db.Notifications
            .Where(n => n.Id == notificationId && n.RecipientId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}