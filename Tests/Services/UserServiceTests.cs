using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Groovo.Data.Contexts;
using Groovo.Models;
using Groovo.Services;
using Groovo.DTOs.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Groovo.Tests.Services;

public class UserServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<UserService>> _loggerMock;
    private readonly UserService _service;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);

        _loggerMock = new Mock<ILogger<UserService>>();
        _service = new UserService(_context, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAllUsersAsync_Returns_All_Users()
    {
        _context.Users.AddRange(
            new User { Id = Guid.NewGuid(), Name = "A", Role = UserRole.Author },
            new User { Id = Guid.NewGuid(), Name = "B", Role = UserRole.User }
        );
        await _context.SaveChangesAsync();

        var result = await _service.GetAllUsersAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllUsersAsync_Filters_By_Role()
    {
        _context.Users.AddRange(
            new User { Id = Guid.NewGuid(), Name = "A", Role = UserRole.Author },
            new User { Id = Guid.NewGuid(), Name = "B", Role = UserRole.User }
        );
        await _context.SaveChangesAsync();

        var result = await _service.GetAllUsersAsync(UserRole.Author);

        Assert.Single(result);
        Assert.Equal(UserRole.Author, result[0].Role);
    }

    [Fact]
    public async Task GetAuthorsAsync_Returns_Only_Authors()
    {
        _context.Users.AddRange(
            new User { Id = Guid.NewGuid(), Name = "A", Role = UserRole.Author },
            new User { Id = Guid.NewGuid(), Name = "B", Role = UserRole.User }
        );
        await _context.SaveChangesAsync();

        var result = await _service.GetAuthorsAsync();

        Assert.Single(result);
        Assert.Equal("A", result[0].Name);
    }

    [Fact]
    public async Task GetUserByIdAsync_Returns_User()
    {
        var id = Guid.NewGuid();

        _context.Users.Add(new User
        {
            Id = id,
            Name = "TestUser",
            Bio = "Bio",
            Role = UserRole.Author
        });

        await _context.SaveChangesAsync();

        var result = await _service.GetUserByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("TestUser", result!.Name);
    }

    [Fact]
    public async Task GetUserByIdAsync_Returns_Null_When_Not_Found()
    {
        var result = await _service.GetUserByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateUserAsync_Updates_Existing_User()
    {
        var id = Guid.NewGuid();
        _context.Users.Add(new User { Id = id, Name = "Old", Bio = "OldBio" });

        await _context.SaveChangesAsync();

        var success = await _service.UpdateUserAsync(id, "New", "NewBio", "new.png");

        Assert.True(success);

        var updated = await _context.Users.FirstAsync(u => u.Id == id);
        Assert.Equal("New", updated.Name);
        Assert.Equal("NewBio", updated.Bio);
        Assert.Equal("new.png", updated.ImageUrl);
    }

    [Fact]
    public async Task UpdateUserAsync_Returns_False_When_User_Not_Found()
    {
        var result = await _service.UpdateUserAsync(Guid.NewGuid(), "Test", null, null);
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteUserAsync_Deletes_User_And_Relations()
    {
        var id = Guid.NewGuid();

        var user = new User { Id = id, Name = "TestUser" };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _service.DeleteUserAsync(id);

        Assert.True(result);
        Assert.False(_context.Users.Any());
    }

    [Fact]
    public async Task DeleteUserAsync_Returns_False_When_Not_Found()
    {
        var result = await _service.DeleteUserAsync(Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task GetAuthorByIdAsync_Returns_Author()
    {
        var id = Guid.NewGuid();

        _context.Users.Add(new User
        {
            Id = id,
            Name = "Author",
            Role = UserRole.Author
        });

        await _context.SaveChangesAsync();

        var result = await _service.GetAuthorByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal(id, result!.Id);
    }

    [Fact]
    public async Task GetAuthorByIdAsync_Returns_Null_If_User_Not_Author()
    {
        var id = Guid.NewGuid();
        _context.Users.Add(new User { Id = id, Name = "NotAuthor", Role = UserRole.User });
        await _context.SaveChangesAsync();

        var result = await _service.GetAuthorByIdAsync(id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAuthorSongsAsync_Returns_Author_Songs()
    {
        var authorId = Guid.NewGuid();

        var author = new User { Id = authorId, Name = "Auth", Role = UserRole.Author };
        var song = new Song { Id = Guid.NewGuid(), Name = "Track", IsActive = true };

        _context.Users.Add(author);
        _context.Songs.Add(song);

        _context.SongAuthors.Add(new SongAuthor
        {
            SongId = song.Id,
            UserId = authorId
        });

        await _context.SaveChangesAsync();

        var result = await _service.GetAuthorSongsAsync(authorId);

        Assert.Single(result);
        Assert.Equal("Track", result[0].Name);
    }

    [Fact]
    public async Task GetUserPlaylistsAsync_Returns_Playlists()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Name = "Test" };

        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Name = "Playlist1",
            IsPublic = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.Playlists.Add(playlist);

        _context.PlaylistOwners.Add(new PlaylistOwner
        {
            PlaylistId = playlist.Id,
            UserId = userId
        });

        await _context.SaveChangesAsync();

        var result = await _service.GetUserPlaylistsAsync(userId, showFullList: true);

        Assert.Single(result);
        Assert.Equal("Playlist1", result[0].Name);
    }

    [Fact]
    public async Task SearchUsersAsync_Returns_Matching_Users()
    {
        _context.Users.AddRange(
            new User { Id = Guid.NewGuid(), Name = "Alice", Role = UserRole.User },
            new User { Id = Guid.NewGuid(), Name = "Bob", Bio = "guitar player", Role = UserRole.User }
        );

        await _context.SaveChangesAsync();

        var result = await _service.SearchUsersAsync("guitar");

        Assert.Single(result);
        Assert.Equal("Bob", result[0].Name);
    }
}
