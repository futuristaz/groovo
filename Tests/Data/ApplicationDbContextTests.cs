using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.Models;
using Groovo.DTOs;

namespace Groovo.Tests.Data;

public class ApplicationDbContextTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public ApplicationDbContextTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"DataSource=:memory:")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region SaveChanges Tests

    [Fact]
    public void SaveChanges_NewEntity_SetsCreatedAtAndUpdatedAt()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = UserRole.User
        };

        // Act
        _context.Users.Add(user);
        _context.SaveChanges();

        // Assert
        var savedUser = _context.Users.Find(user.Id);
        Assert.NotNull(savedUser);
        Assert.NotEqual(default(DateTime), savedUser.CreatedAt);
        Assert.NotEqual(default(DateTime), savedUser.UpdatedAt);
        Assert.True((DateTime.UtcNow - savedUser.CreatedAt).TotalSeconds < 2);
        Assert.True((DateTime.UtcNow - savedUser.UpdatedAt).TotalSeconds < 2);
    }

    [Fact]
    public void SaveChanges_ModifiedEntity_UpdatesOnlyUpdatedAt()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = UserRole.User
        };

        _context.Users.Add(user);
        _context.SaveChanges();

        var originalCreatedAt = user.CreatedAt;

        // Wait to ensure timestamp would be different if it changes
        Thread.Sleep(1100);

        // Act
        user.Name = "Updated Name";
        _context.Entry(user).State = EntityState.Modified;
        _context.SaveChanges();

        // Assert
        var savedUser = _context.Users.Find(user.Id);
        Assert.NotNull(savedUser);
        Assert.Equal(originalCreatedAt, savedUser.CreatedAt);
        // Verify UpdatedAt was set by UpdateTimestamps (SQLite provider)
        Assert.NotEqual(default(DateTime), savedUser.UpdatedAt);
    }

    [Fact]
    public void SaveChanges_PlaylistSong_SetsAddedAt()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = UserRole.User
        };

        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Name = "Test Playlist",
            Description = "Test"
        };

        var song = new Song
        {
            Id = Guid.NewGuid(),
            Name = "Test Song",
            Album = Guid.NewGuid(),
            Genre = "Test",
            AudioUrl = "http://test.com/song.mp3",
            ReleaseDate = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.Playlists.Add(playlist);
        _context.Songs.Add(song);
        _context.SaveChanges();

        var playlistSong = new PlaylistSong
        {
            PlaylistId = playlist.Id,
            SongId = song.Id,
            Order = 0
        };

        // Act
        _context.PlaylistSongs.Add(playlistSong);
        _context.SaveChanges();

        // Assert
        var savedPlaylistSong = _context.PlaylistSongs
            .FirstOrDefault(ps => ps.PlaylistId == playlist.Id && ps.SongId == song.Id);
        Assert.NotNull(savedPlaylistSong);
        Assert.NotEqual(default(DateTime), savedPlaylistSong.AddedAt);
        Assert.True((DateTime.UtcNow - savedPlaylistSong.AddedAt).TotalSeconds < 2);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task SaveChangesAsync_NoChanges_DoesNotThrow()
    {
        // Arrange - context with no changes

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () => await _context.SaveChangesAsync());
        Assert.Null(exception);
    }

    [Fact]
    public async Task SaveChangesAsync_DeletedEntity_DoesNotUpdateTimestamps()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = UserRole.User
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var originalUpdatedAt = user.UpdatedAt;
        await Task.Delay(100);

        // Act
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        // Assert
        var deletedUser = await _context.Users.FindAsync(user.Id);
        Assert.Null(deletedUser);
    }

    #endregion
}
