using Microsoft.Extensions.Logging;
using Moq;
using Groovo.Services;
using Groovo.Repositories;
using Groovo.Models;
using Groovo.DTOs;

namespace Groovo.Tests.Services;

public class AuthorServiceTests
{
    private readonly Mock<ILogger<AuthorService>> _loggerMock;
    private readonly Mock<ISongRepository> _songRepositoryMock;
    private readonly AuthorService _service;

    public AuthorServiceTests()
    {
        _loggerMock = new Mock<ILogger<AuthorService>>();
        _songRepositoryMock = new Mock<ISongRepository>();
        _service = new AuthorService(_loggerMock.Object, _songRepositoryMock.Object);
    }

    [Fact]
    public async Task GetAuthorSongByIdAsync_ReturnsSong_WhenUserIsAuthor()
    {
        var userId = Guid.NewGuid();
        var songId = Guid.NewGuid();
        var author = new User 
        { 
            Id = userId, 
            Name = "Test Author", 
            Role = UserRole.Author,
            Bio = "Test bio",
            ImageUrl = "test.jpg"
        };
        var song = new Song
        {
            Id = songId,
            Name = "Test Song",
            Description = "Test Description",
            ReleaseDate = DateTime.UtcNow,
            Picture = "test.jpg",
            Album = Guid.NewGuid(),
            Genre = "Rock",
            Tags = "tag1,tag2",
            AudioUrl = "audio.mp3",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Length = 180,
            Plays = 100,
            Likes = 50,
            SongAuthors = new List<SongAuthor>
            {
                new SongAuthor { UserId = userId, SongId = songId, User = author }
            }
        };

        _songRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(song);

        var result = await _service.GetAuthorSongByIdAsync(songId, userId, false);

        Assert.NotNull(result);
        Assert.Equal(songId, result.Id);
        Assert.Equal("Test Song", result.Name);
        Assert.Single(result.Authors);
        Assert.Equal(userId, result.Authors[0].Id);
        Assert.Equal("Test Author", result.Authors[0].Name);
    }

    [Fact]
    public async Task GetAuthorSongByIdAsync_ReturnsSong_WhenUserIsAdmin()
    {
        var userId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var songId = Guid.NewGuid();
        var author = new User 
        { 
            Id = authorId, 
            Name = "Song Author", 
            Role = UserRole.Author,
            Bio = "Author bio",
            ImageUrl = "author.jpg"
        };
        var song = new Song
        {
            Id = songId,
            Name = "Test Song",
            Description = "Test Description",
            ReleaseDate = DateTime.UtcNow,
            Picture = "test.jpg",
            Album = Guid.NewGuid(),
            Genre = "Rock",
            Tags = "tag1,tag2",
            AudioUrl = "audio.mp3",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Length = 180,
            Plays = 100,
            Likes = 50,
            SongAuthors = new List<SongAuthor>
            {
                new SongAuthor { UserId = authorId, SongId = songId, User = author }
            }
        };

        _songRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(song);

        var result = await _service.GetAuthorSongByIdAsync(songId, userId, true);

        Assert.NotNull(result);
        Assert.Equal(songId, result.Id);
        Assert.Equal("Test Song", result.Name);
        Assert.Single(result.Authors);
        Assert.Equal(authorId, result.Authors[0].Id);
    }

    [Fact]
    public async Task GetAuthorSongByIdAsync_ReturnsNull_WhenSongDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var songId = Guid.NewGuid();

        _songRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync((Song?)null);

        var result = await _service.GetAuthorSongByIdAsync(songId, userId, false);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAuthorSongByIdAsync_ReturnsNull_WhenUserIsNotAuthorAndNotAdmin()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var songId = Guid.NewGuid();
        var author = new User 
        { 
            Id = otherUserId, 
            Name = "Other Author", 
            Role = UserRole.Author 
        };
        var song = new Song
        {
            Id = songId,
            Name = "Test Song",
            Description = "Test Description",
            ReleaseDate = DateTime.UtcNow,
            Picture = "test.jpg",
            Album = Guid.NewGuid(),
            Genre = "Rock",
            Tags = "tag1,tag2",
            AudioUrl = "audio.mp3",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Length = 180,
            Plays = 100,
            Likes = 50,
            SongAuthors = new List<SongAuthor>
            {
                new SongAuthor { UserId = otherUserId, SongId = songId, User = author }
            }
        };

        _songRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(song);

        var result = await _service.GetAuthorSongByIdAsync(songId, userId, false);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAuthorSongByIdAsync_FiltersOnlyAuthorRole_InResponse()
    {
        var userId = Guid.NewGuid();
        var songId = Guid.NewGuid();
        var author1 = new User 
        { 
            Id = userId, 
            Name = "Author 1", 
            Role = UserRole.Author 
        };
        var author2 = new User 
        { 
            Id = Guid.NewGuid(), 
            Name = "Author 2", 
            Role = UserRole.Author 
        };
        var regularUser = new User 
        { 
            Id = Guid.NewGuid(), 
            Name = "Regular User", 
            Role = UserRole.User 
        };
        var song = new Song
        {
            Id = songId,
            Name = "Test Song",
            Description = "Test Description",
            ReleaseDate = DateTime.UtcNow,
            Picture = "test.jpg",
            Album = Guid.NewGuid(),
            Genre = "Rock",
            Tags = "tag1,tag2",
            AudioUrl = "audio.mp3",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Length = 180,
            Plays = 100,
            Likes = 50,
            SongAuthors = new List<SongAuthor>
            {
                new SongAuthor { UserId = userId, SongId = songId, User = author1 },
                new SongAuthor { UserId = author2.Id, SongId = songId, User = author2 },
                new SongAuthor { UserId = regularUser.Id, SongId = songId, User = regularUser }
            }
        };

        _songRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(song);

        var result = await _service.GetAuthorSongByIdAsync(songId, userId, false);

        Assert.NotNull(result);
        Assert.Equal(2, result.Authors.Count);
        Assert.All(result.Authors, a => Assert.NotEqual("Regular User", a.Name));
    }

    [Fact]
    public async Task GetAuthorSongByIdAsync_ThrowsAndLogs_WhenRepositoryThrows()
    {
        var userId = Guid.NewGuid();
        var songId = Guid.NewGuid();
        var exception = new Exception("Database error");

        _songRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(exception);

        await Assert.ThrowsAsync<Exception>(() => 
            _service.GetAuthorSongByIdAsync(songId, userId, false));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error retrieving song {songId}")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAuthorSongByIdAsync_ReturnsSong_WhenUserIsOneOfMultipleAuthors()
    {
        var userId = Guid.NewGuid();
        var otherAuthorId = Guid.NewGuid();
        var songId = Guid.NewGuid();
        var author1 = new User 
        { 
            Id = userId, 
            Name = "Author 1", 
            Role = UserRole.Author 
        };
        var author2 = new User 
        { 
            Id = otherAuthorId, 
            Name = "Author 2", 
            Role = UserRole.Author 
        };
        var song = new Song
        {
            Id = songId,
            Name = "Collaborative Song",
            Description = "Test Description",
            ReleaseDate = DateTime.UtcNow,
            Picture = "test.jpg",
            Album = Guid.NewGuid(),
            Genre = "Rock",
            Tags = "tag1,tag2",
            AudioUrl = "audio.mp3",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Length = 180,
            Plays = 100,
            Likes = 50,
            SongAuthors = new List<SongAuthor>
            {
                new SongAuthor { UserId = userId, SongId = songId, User = author1 },
                new SongAuthor { UserId = otherAuthorId, SongId = songId, User = author2 }
            }
        };

        _songRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(song);

        var result = await _service.GetAuthorSongByIdAsync(songId, userId, false);

        Assert.NotNull(result);
        Assert.Equal(2, result.Authors.Count);
    }

    [Fact]
    public async Task GetAuthorSongByIdAsync_IncludesAuthorDetails_InResponse()
    {
        var userId = Guid.NewGuid();
        var songId = Guid.NewGuid();
        var author = new User 
        { 
            Id = userId, 
            Name = "Test Author", 
            Role = UserRole.Author,
            Bio = "Detailed bio",
            ImageUrl = "profile.jpg"
        };
        var song = new Song
        {
            Id = songId,
            Name = "Test Song",
            Description = "Test Description",
            ReleaseDate = DateTime.UtcNow,
            Picture = "test.jpg",
            Album = Guid.NewGuid(),
            Genre = "Rock",
            Tags = "tag1,tag2",
            AudioUrl = "audio.mp3",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Length = 180,
            Plays = 100,
            Likes = 50,
            SongAuthors = new List<SongAuthor>
            {
                new SongAuthor { UserId = userId, SongId = songId, User = author }
            }
        };

        _songRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(song);

        var result = await _service.GetAuthorSongByIdAsync(songId, userId, false);

        Assert.NotNull(result);
        Assert.Single(result.Authors);
        var authorResponse = result.Authors[0];
        Assert.Equal(userId, authorResponse.Id);
        Assert.Equal("Test Author", authorResponse.Name);
        Assert.Equal("Detailed bio", authorResponse.Bio);
        Assert.Equal("profile.jpg", authorResponse.ImageUrl);
    }
}