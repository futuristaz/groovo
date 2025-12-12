using Groovo.Models;
using Groovo.Services;
using Groovo.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace Groovo.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<ILogger<UserService>> _loggerMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _loggerMock = new Mock<ILogger<UserService>>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _service = new UserService(_loggerMock.Object, _userRepositoryMock.Object);
    }

    [Fact]
    public async Task GetUserByIdAsync_Returns_User()
    {
        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Name = "TestUser",
            Bio = "Bio",
            Role = UserRole.Author
        };

        _userRepositoryMock.Setup(r => r.GetByAsync(id, null))
            .ReturnsAsync(user);

        var result = await _service.GetUserByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("TestUser", result!.Name);
    }

    [Fact]
    public async Task GetUserByIdAsync_Returns_Null_When_Not_Found()
    {
        var id = Guid.NewGuid();
        
        _userRepositoryMock.Setup(r => r.GetByAsync(id, null))
            .ReturnsAsync((User?)null);
            
        var result = await _service.GetUserByIdAsync(id);
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateUserAsync_Updates_Existing_User()
    {
        var id = Guid.NewGuid();
        var user = new User { Id = id, Name = "Old", Bio = "OldBio" };
        
        _userRepositoryMock.Setup(r => r.GetByAsync(id, null))
            .ReturnsAsync(user);

        User? updatedUser = null;
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>()))
            .Callback<User>(u => updatedUser = u)
            .Returns(Task.CompletedTask);

        var success = await _service.UpdateUserAsync(id, "New", "NewBio", "new.png");

        Assert.True(success);
        Assert.Equal("New", updatedUser!.Name);
        Assert.Equal("NewBio", updatedUser.Bio);
        Assert.Equal("new.png", updatedUser.ImageUrl);
    }

    [Fact]
    public async Task UpdateUserAsync_Returns_False_When_User_Not_Found()
    {
        var id = Guid.NewGuid();
        
        _userRepositoryMock.Setup(r => r.GetByAsync(id, null))
            .ReturnsAsync((User?)null);
            
        var result = await _service.UpdateUserAsync(id, "Test", null, null);
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteUserAsync_Deletes_User_And_Relations()
    {
        var id = Guid.NewGuid();
        var user = new User { Id = id, Name = "TestUser" };

        _userRepositoryMock.Setup(r => r.GetByAsync(id, null))
            .ReturnsAsync(user);

        _userRepositoryMock.Setup(r => r.DeleteAsync(id))
            .Returns(Task.CompletedTask);

        var result = await _service.DeleteUserAsync(id);

        Assert.True(result);
        _userRepositoryMock.Verify(r => r.DeleteAsync(id), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_Returns_False_When_Not_Found()
    {
        var id = Guid.NewGuid();
        
        _userRepositoryMock.Setup(r => r.GetByAsync(id, null))
            .ReturnsAsync((User?)null);
            
        var result = await _service.DeleteUserAsync(id);
        Assert.False(result);
    }

    [Fact]
    public async Task GetAuthorByIdAsync_Returns_Author()
    {
        var id = Guid.NewGuid();
        var author = new User
        {
            Id = id,
            Name = "Author",
            Role = UserRole.Author
        };

        _userRepositoryMock.Setup(r => r.GetByAsync(id, null))
            .ReturnsAsync(author);

        var result = await _service.GetAuthorByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal(id, result!.Id);
    }

    [Fact]
    public async Task GetAuthorByIdAsync_Returns_Null_If_User_Not_Author()
    {
        var id = Guid.NewGuid();
        var nonAuthor = new User { Id = id, Name = "NotAuthor", Role = UserRole.User };
        
        _userRepositoryMock.Setup(r => r.GetByAsync(id, null))
            .ReturnsAsync(nonAuthor);

        var result = await _service.GetAuthorByIdAsync(id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAuthorSongsAsync_Returns_Author_Songs()
    {
        var authorId = Guid.NewGuid();
        var song = new Song { Id = Guid.NewGuid(), Name = "Track", IsActive = true };
        var author = new User 
        { 
            Id = authorId, 
            Name = "Auth", 
            Role = UserRole.Author,
            SongAuthors = new List<SongAuthor>
            {
                new SongAuthor { SongId = song.Id, UserId = authorId, Song = song }
            }
        };

        _userRepositoryMock.Setup(r => r.GetAuthorWithSongsAsync(authorId))
            .ReturnsAsync(author);

        var result = await _service.GetAuthorSongsAsync(authorId);

        Assert.Single(result);
        Assert.Equal("Track", result[0].Name);
    }

    [Fact]
    public async Task GetUserPlaylistsAsync_Returns_Playlists()
    {
        var userId = Guid.NewGuid();
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Name = "Playlist1",
            IsPublic = true,
            CreatedAt = DateTime.UtcNow
        };
        
        var user = new User 
        { 
            Id = userId, 
            Name = "Test",
            PlaylistOwners = new List<PlaylistOwner>
            {
                new PlaylistOwner { PlaylistId = playlist.Id, UserId = userId, Playlist = playlist }
            }
        };

        _userRepositoryMock.Setup(r => r.GetUserWithPlaylistsAsync(userId))
            .ReturnsAsync(user);

        var result = await _service.GetUserPlaylistsAsync(userId, showFullList: true);

        Assert.Single(result);
        Assert.Equal("Playlist1", result[0].Name);
    }

    [Fact]
    public async Task SearchUsersAsync_Returns_Matching_Users()
    {
        var bob = new User { Id = Guid.NewGuid(), Name = "Bob", Bio = "guitar player", Role = UserRole.User };
        
        _userRepositoryMock.Setup(r => r.SearchAsync("guitar", null))
            .ReturnsAsync(new List<User> { bob });

        var result = await _service.SearchUsersAsync("guitar");

        Assert.Single(result);
        Assert.Equal("Bob", result[0].Name);
    }
}
