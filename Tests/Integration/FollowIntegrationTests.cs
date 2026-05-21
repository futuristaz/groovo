using Groovo.Data.Contexts;
using Groovo.Models;
using Groovo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Integration;

/// <summary>
/// Integration tests for the Follow Module.
/// Covers one-way follow notifications, mutual friendship detection,
/// unfollow cleanup, and friends list filtering.
/// </summary>
public class FollowIntegrationTests : IClassFixture<GroovoWebFactory>
{
    private readonly GroovoWebFactory _factory;

    public FollowIntegrationTests(GroovoWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Follow_WhenMutual_CreatesFriendNotificationsForBothUsers()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var followService = scope.ServiceProvider.GetRequiredService<IFollowService>();

        // Cleanup leftover data from other tests in this class
        db.Notifications.RemoveRange(db.Notifications);
        db.UserFollows.RemoveRange(db.UserFollows);
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();

        // Arrange
        var userA = new User { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@test.com", PasswordHash = "hash" };
        var userB = new User { Id = Guid.NewGuid(), Name = "Bob",   Email = "bob@test.com",   PasswordHash = "hash" };
        db.Users.AddRange(userA, userB);

        // B already follows A
        db.UserFollows.Add(new UserFollow
        {
            FollowerId = userB.Id,
            FollowedId = userA.Id,
            CreatedAt  = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act — A follows B back, making it mutual
        var result = await followService.FollowAsync(userA.Id, userB.Id);

        // Assert — both users receive a NewFriend notification
        Assert.True(result);

        var notifications = await db.Notifications.ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, n => Assert.Equal(NotificationType.NewFriend, n.Type));
        Assert.Contains(notifications, n => n.RecipientId == userA.Id);
        Assert.Contains(notifications, n => n.RecipientId == userB.Id);
    }

    [Fact]
    public async Task Follow_WhenNotMutual_CreatesNewFollowerNotificationOnlyForTarget()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var followService = scope.ServiceProvider.GetRequiredService<IFollowService>();

        // Arrange — no existing follow relationship
        var userA = new User { Id = Guid.NewGuid(), Name = "Charlie", Email = "charlie@test.com", PasswordHash = "hash" };
        var userB = new User { Id = Guid.NewGuid(), Name = "Diana",   Email = "diana@test.com",   PasswordHash = "hash" };
        db.Users.AddRange(userA, userB);
        await db.SaveChangesAsync();

        // Act — A follows B (not mutual)
        var result = await followService.FollowAsync(userA.Id, userB.Id);

        // Assert — only B gets a NewFollower notification
        Assert.True(result);

        var notifications = await db.Notifications.ToListAsync();
        Assert.Single(notifications);
        Assert.Equal(NotificationType.NewFollower, notifications[0].Type);
        Assert.Equal(userB.Id, notifications[0].RecipientId);
        Assert.Equal(userA.Id, notifications[0].ActorId);
    }

    [Fact]
    public async Task Unfollow_WhenFollowExists_RemovesFollowRecord()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var followService = scope.ServiceProvider.GetRequiredService<IFollowService>();

        // Arrange — A follows B
        var userA = new User { Id = Guid.NewGuid(), Name = "Eve",   Email = "eve@test.com",   PasswordHash = "hash" };
        var userB = new User { Id = Guid.NewGuid(), Name = "Frank", Email = "frank@test.com", PasswordHash = "hash" };
        db.Users.AddRange(userA, userB);
        db.UserFollows.Add(new UserFollow
        {
            FollowerId = userA.Id,
            FollowedId = userB.Id,
            CreatedAt  = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var result = await followService.UnfollowAsync(userA.Id, userB.Id);

        // Assert — follow record is removed
        Assert.True(result);

        var followExists = await db.UserFollows
            .AnyAsync(f => f.FollowerId == userA.Id && f.FollowedId == userB.Id);
        Assert.False(followExists);
    }

    [Fact]
    public async Task GetFriends_ReturnsOnlyMutualFollows_NotOneWay()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var followService = scope.ServiceProvider.GetRequiredService<IFollowService>();

        // Arrange — A ↔ B mutual, A → C one-way
        var userA = new User { Id = Guid.NewGuid(), Name = "Grace", Email = "grace@test.com", PasswordHash = "hash" };
        var userB = new User { Id = Guid.NewGuid(), Name = "Henry", Email = "henry@test.com", PasswordHash = "hash" };
        var userC = new User { Id = Guid.NewGuid(), Name = "Isla",  Email = "isla@test.com",  PasswordHash = "hash" };
        db.Users.AddRange(userA, userB, userC);
        db.UserFollows.AddRange(
            new UserFollow { FollowerId = userA.Id, FollowedId = userB.Id, CreatedAt = DateTime.UtcNow },
            new UserFollow { FollowerId = userB.Id, FollowedId = userA.Id, CreatedAt = DateTime.UtcNow },
            new UserFollow { FollowerId = userA.Id, FollowedId = userC.Id, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        // Act
        var friends = await followService.GetFriendsAsync(userA.Id);

        // Assert — only B is a friend, C is excluded (one-way follow)
        Assert.NotNull(friends);
        Assert.Single(friends);
        Assert.Equal(userB.Id, friends![0].Id);
    }
}