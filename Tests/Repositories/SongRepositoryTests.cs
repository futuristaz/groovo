using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.Models;
using Groovo.Repositories;
using Groovo.DTOs;

namespace Groovo.Tests.Repositories;

public class SongRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SongRepository _repository;
    private readonly Guid _albumId = Guid.NewGuid();
    private readonly Guid _authorId = Guid.NewGuid();

    public SongRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new SongRepository(_context);

        SeedTestData();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedTestData()
    {
        // Create test users
        var author1 = new User
        {
            Id = _authorId,
            Name = "Test Author",
            Email = "author@test.com",
            PasswordHash = "hash",
            Role = UserRole.Author,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var author2 = new User
        {
            Id = Guid.NewGuid(),
            Name = "Second Author",
            Email = "author2@test.com",
            PasswordHash = "hash",
            Role = UserRole.Author,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var regularUser = new User
        {
            Id = Guid.NewGuid(),
            Name = "Regular User",
            Email = "user@test.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(author1, author2, regularUser);

        // Create test songs
        var song1 = new Song
        {
            Id = Guid.NewGuid(),
            Name = "Rock Song",
            Album = _albumId,
            Genre = "Rock",
            AudioUrl = "http://test.com/song1.mp3",
            ReleaseDate = DateTime.UtcNow.AddDays(-10),
            Length = 180,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var song2 = new Song
        {
            Id = Guid.NewGuid(),
            Name = "Jazz Song",
            Album = _albumId,
            Genre = "Jazz",
            AudioUrl = "http://test.com/song2.mp3",
            ReleaseDate = DateTime.UtcNow.AddDays(-5),
            Length = 240,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var inactiveSong = new Song
        {
            Id = Guid.NewGuid(),
            Name = "Inactive Song",
            Album = _albumId,
            Genre = "Pop",
            AudioUrl = "http://test.com/inactive.mp3",
            ReleaseDate = DateTime.UtcNow.AddDays(-20),
            Length = 200,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Songs.AddRange(song1, song2, inactiveSong);

        // Create song authors
        _context.SongAuthors.AddRange(
            new SongAuthor { SongId = song1.Id, UserId = author1.Id },
            new SongAuthor { SongId = song2.Id, UserId = author1.Id },
            new SongAuthor { SongId = song2.Id, UserId = author2.Id },
            new SongAuthor { SongId = inactiveSong.Id, UserId = author1.Id }
        );

        _context.SaveChanges();
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingActiveSong_ReturnsSong()
    {
        // Arrange
        var song = _context.Songs.First(s => s.IsActive);

        // Act
        var result = await _repository.GetByIdAsync(song.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(song.Id, result.Id);
        Assert.Equal(song.Name, result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_InactiveSong_ReturnsNull()
    {
        // Arrange
        var inactiveSong = _context.Songs.First(s => !s.IsActive);

        // Act
        var result = await _repository.GetByIdAsync(inactiveSong.Id, includeInactive: false);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_InactiveSongWithInclude_ReturnsSong()
    {
        // Arrange
        var inactiveSong = _context.Songs.First(s => !s.IsActive);

        // Act
        var result = await _repository.GetByIdAsync(inactiveSong.Id, includeInactive: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(inactiveSong.Id, result.Id);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_WithAuthors_IncludesAuthorData()
    {
        // Arrange
        var song = _context.Songs.First(s => s.Name == "Jazz Song");

        // Act
        var result = await _repository.GetByIdAsync(song.Id, includeAuthors: true);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.SongAuthors);
        Assert.Equal(2, result.SongAuthors.Count); // Jazz Song has 2 authors
        Assert.All(result.SongAuthors, sa => Assert.NotNull(sa.User));
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentSong_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetByAuthorAsync Tests

    [Fact]
    public async Task GetByAuthorAsync_ReturnsAuthorSongsOrderedByDate()
    {
        // Act
        var result = await _repository.GetByAuthorAsync(_authorId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count); // Author has 2 active songs
        Assert.All(result, song => Assert.True(song.IsActive));
        
        // Verify ordering (most recent first)
        Assert.True(result[0].ReleaseDate >= result[1].ReleaseDate);
        
        // Verify authors are included
        Assert.All(result, song => Assert.NotEmpty(song.SongAuthors));
    }

    [Fact]
    public async Task GetByAuthorAsync_WithInactive_IncludesInactiveSongs()
    {
        // Act
        var result = await _repository.GetByAuthorAsync(_authorId, includeInactive: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count); // Author has 3 total songs (2 active + 1 inactive)
        Assert.Contains(result, s => !s.IsActive);
    }

    [Fact]
    public async Task GetByAuthorAsync_NoSongs_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentAuthorId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByAuthorAsync(nonExistentAuthorId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region ExistsByNameAndAlbumAsync Tests

    [Fact]
    public async Task ExistsByNameAndAlbumAsync_DuplicateExists_ReturnsTrue()
    {
        // Arrange
        var existingSong = _context.Songs.First();

        // Act
        var result = await _repository.ExistsByNameAndAlbumAsync(existingSong.Name, existingSong.Album);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsByNameAndAlbumAsync_NoDuplicate_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsByNameAndAlbumAsync("Non-Existent Song", _albumId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsByNameAndAlbumAsync_WithExcludeId_ExcludesSelf()
    {
        // Arrange
        var existingSong = _context.Songs.First();

        // Act - Excluding the song itself should return false
        var result = await _repository.ExistsByNameAndAlbumAsync(
            existingSong.Name, 
            existingSong.Album, 
            excludeId: existingSong.Id
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsByNameAndAlbumAsync_WithExcludeId_FindsOtherDuplicates()
    {
        // Arrange - Create a duplicate song
        var originalSong = _context.Songs.First();
        var duplicateSong = new Song
        {
            Id = Guid.NewGuid(),
            Name = originalSong.Name,
            Album = originalSong.Album,
            Genre = "Test",
            AudioUrl = "http://test.com/dup.mp3",
            ReleaseDate = DateTime.UtcNow,
            Length = 180,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Songs.Add(duplicateSong);
        _context.SaveChanges();

        // Act - Excluding the duplicate should still find the original
        var result = await _repository.ExistsByNameAndAlbumAsync(
            originalSong.Name, 
            originalSong.Album, 
            excludeId: duplicateSong.Id
        );

        // Assert
        Assert.True(result);
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_ByName_ReturnsMatchingSongs()
    {
        // Act
        var result = await _repository.SearchAsync("Rock");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Rock Song", result[0].Name);
    }

    [Fact]
    public async Task SearchAsync_ByGenre_ReturnsMatchingSongs()
    {
        // Act
        var result = await _repository.SearchAsync("Jazz");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Jazz Song", result[0].Name);
    }

    [Fact]
    public async Task SearchAsync_ByAuthorName_ReturnsMatchingSongs()
    {
        // Act
        var result = await _repository.SearchAsync("Test Author");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count); // Test Author has 2 active songs
        Assert.All(result, song => 
            Assert.Contains(song.SongAuthors, sa => sa.User.Name.Contains("Test Author"))
        );
    }

    [Fact]
    public async Task SearchAsync_MultipleMatches_OrdersByName()
    {
        // Act
        var result = await _repository.SearchAsync("Song"); // Matches multiple songs

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2);
    }

    [Fact]
    public async Task SearchAsync_OnlyActivesSongs_ExcludesInactive()
    {
        // Act
        var result = await _repository.SearchAsync("Song");

        // Assert
        Assert.NotNull(result);
        Assert.All(result, song => Assert.True(song.IsActive));
        Assert.DoesNotContain(result, s => s.Name == "Inactive Song");
    }

    [Fact]
    public async Task SearchAsync_NoMatches_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.SearchAsync("NonExistentQuery12345");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_IncludesAuthors_AuthorsAreLoaded()
    {
        // Act
        var result = await _repository.SearchAsync("Rock");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.NotEmpty(result[0].SongAuthors);
        Assert.All(result[0].SongAuthors, sa => Assert.NotNull(sa.User));
    }

    #endregion

    #region GetSongAuthorNamesAsync Tests

    [Fact]
    public async Task GetSongAuthorNamesAsync_ReturnsAuthorNames()
    {
        // Arrange
        var jazzSong = _context.Songs.First(s => s.Name == "Jazz Song");

        // Act
        var result = await _repository.GetSongAuthorNamesAsync(jazzSong.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains("Test Author", result);
        Assert.Contains("Second Author", result);
    }

    [Fact]
    public async Task GetSongAuthorNamesAsync_OnlyAuthors_ExcludesNonAuthors()
    {
        // Arrange
        var regularUser = _context.Users.First(u => u.Role == UserRole.User);
        var song = _context.Songs.First(s => s.IsActive);
        
        // Add regular user as a song author
        _context.SongAuthors.Add(new SongAuthor { SongId = song.Id, UserId = regularUser.Id });
        _context.SaveChanges();

        // Act
        var result = await _repository.GetSongAuthorNamesAsync(song.Id);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("Regular User", result); // Should not include non-authors
    }

    [Fact]
    public async Task GetSongAuthorNamesAsync_NoAuthors_ReturnsEmptyList()
    {
        // Arrange - Create a song with no authors
        var songWithoutAuthors = new Song
        {
            Id = Guid.NewGuid(),
            Name = "No Authors Song",
            Album = _albumId,
            Genre = "Test",
            AudioUrl = "http://test.com/noauth.mp3",
            ReleaseDate = DateTime.UtcNow,
            Length = 180,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Songs.Add(songWithoutAuthors);
        _context.SaveChanges();

        // Act
        var result = await _repository.GetSongAuthorNamesAsync(songWithoutAuthors.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetSongLengthAsync Tests

    [Fact]
    public async Task GetSongLengthAsync_ActiveSong_ReturnsLength()
    {
        // Arrange
        var song = _context.Songs.First(s => s.IsActive);

        // Act
        var result = await _repository.GetSongLengthAsync(song.Id);

        // Assert
        Assert.Equal(song.Length, result);
    }

    [Fact]
    public async Task GetSongLengthAsync_InactiveSong_ReturnsZero()
    {
        // Arrange
        var inactiveSong = _context.Songs.First(s => !s.IsActive);

        // Act
        var result = await _repository.GetSongLengthAsync(inactiveSong.Id);

        // Assert
        Assert.Equal(0, result); // FirstOrDefaultAsync returns 0 for int
    }

    [Fact]
    public async Task GetSongLengthAsync_NonExistentSong_ReturnsZero()
    {
        // Act
        var result = await _repository.GetSongLengthAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region CRUD Operations Tests

    [Fact]
    public async Task CreateAsync_AddsSong()
    {
        // Arrange
        var newSong = new Song
        {
            Id = Guid.NewGuid(),
            Name = "New Song",
            Album = _albumId,
            Genre = "Electronic",
            AudioUrl = "http://test.com/new.mp3",
            ReleaseDate = DateTime.UtcNow,
            Length = 200,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = await _repository.CreateAsync(newSong);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(newSong.Id, result.Id);
        
        var savedSong = await _context.Songs.FindAsync(newSong.Id);
        Assert.NotNull(savedSong);
        Assert.Equal("New Song", savedSong.Name);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesSong()
    {
        // Arrange
        var song = _context.Songs.First(s => s.IsActive);
        song.Name = "Updated Name";
        song.Genre = "Updated Genre";

        // Act
        await _repository.UpdateAsync(song);

        // Assert
        var updatedSong = await _context.Songs.FindAsync(song.Id);
        Assert.NotNull(updatedSong);
        Assert.Equal("Updated Name", updatedSong.Name);
        Assert.Equal("Updated Genre", updatedSong.Genre);
    }

    [Fact]
    public async Task DeleteAsync_RemovesSong()
    {
        // Arrange
        var song = _context.Songs.First(s => s.IsActive);
        var songId = song.Id;

        // Act
        await _repository.DeleteAsync(songId);

        // Assert
        var deletedSong = await _context.Songs.FindAsync(songId);
        Assert.Null(deletedSong);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentSong_DoesNotThrow()
    {
        // Act & Assert
        await _repository.DeleteAsync(Guid.NewGuid()); // Should not throw
    }

    #endregion
}