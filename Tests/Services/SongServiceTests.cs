using Microsoft.Extensions.Logging;
using Moq;
using Groovo.Models;
using Groovo.DTOs.Requests;
using Groovo.Services;
using Groovo.Repositories;
using Groovo.DTOs;

namespace Groovo.Tests.Services;

public class SongServiceTests
{
    private readonly Mock<ILogger<SongService>> _loggerMock;
    private readonly Mock<ISongFileService> _fileServiceMock;
    private readonly Mock<ISongRepository> _songRepositoryMock;
    private readonly Mock<IPlaylistRepository> _playlistRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly SongService _service;

    public SongServiceTests()
    {
        _loggerMock = new Mock<ILogger<SongService>>();
        _fileServiceMock = new Mock<ISongFileService>();
        _songRepositoryMock = new Mock<ISongRepository>();
        _playlistRepositoryMock = new Mock<IPlaylistRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();

        _service = new SongService(
            _loggerMock.Object, 
            _fileServiceMock.Object,
            _songRepositoryMock.Object,
            _playlistRepositoryMock.Object,
            _userRepositoryMock.Object);
    }

    [Fact]
    public async Task GetSongByIdAsync_ReturnsSong_WhenSongExists()
    {
        var song = new Song { Id = Guid.NewGuid(), Name = "Test Song", IsActive = true, SongAuthors = new List<SongAuthor>() };
        
        _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, false, true, false))
            .ReturnsAsync(song);

        var result = await _service.GetSongByIdAsync(song.Id);

        Assert.NotNull(result);
        Assert.Equal("Test Song", result!.Name);
    }

    [Fact]
    public async Task GetSongByIdAsync_ReturnsNull_WhenSongDoesNotExist()
    {
        var songId = Guid.NewGuid();
        
        _songRepositoryMock.Setup(r => r.GetByIdAsync(songId, false, false, false))
            .ReturnsAsync((Song?)null);
            
        var result = await _service.GetSongByIdAsync(songId);
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateSongAsync_ReturnsError_WhenAudioFileMissing()
    {
        var album = new Playlist { Id = Guid.NewGuid(), Name = "Album", IsAlbum = true };
        
        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(album.Id, false, false))
            .ReturnsAsync(album);

        var request = new CreateSongRequest
        {
            Name = "New Song",
            AudioId = Guid.NewGuid().ToString(),
            ImageId = Guid.NewGuid().ToString(),
            Album = album.Id,
            AuthorIds = new List<Guid>()
        };

        _fileServiceMock.Setup(f => f.FileExistsAsync(request.AudioId))
            .ReturnsAsync(false);

        var (song, error) = await _service.CreateSongAsync(request, Guid.NewGuid(), isAdmin: true);

        Assert.Null(song);
        Assert.Contains("Audio file not found", error!);
    }

    [Fact]
    public async Task CreateSongAsync_ReturnsError_WhenImageFileMissing()
    {
        var album = new Playlist { Id = Guid.NewGuid(), Name = "Album", IsAlbum = true };
        
        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(album.Id, false, false))
            .ReturnsAsync(album);

        var request = new CreateSongRequest
        {
            Name = "New Song",
            AudioId = Guid.NewGuid().ToString(),
            ImageId = Guid.NewGuid().ToString(),
            Album = album.Id,
            AuthorIds = new List<Guid>()
        };

        _fileServiceMock.Setup(f => f.FileExistsAsync(request.AudioId))
            .ReturnsAsync(true);
        _fileServiceMock.Setup(f => f.FileExistsAsync(request.ImageId))
            .ReturnsAsync(false);

        var (song, error) = await _service.CreateSongAsync(request, Guid.NewGuid(), isAdmin: true);

        Assert.Null(song);
        Assert.Contains("Image file not found", error!);
    }

    [Fact]
    public async Task CreateSongAsync_ReturnsSong_WhenValid()
    {
        var album = new Playlist { Id = Guid.NewGuid(), Name = "Album", IsAlbum = true };
        
        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(album.Id, false, false))
            .ReturnsAsync(album);

        _playlistRepositoryMock.Setup(r => r.GetMaxSongOrderAsync(album.Id))
            .ReturnsAsync(0);

        _playlistRepositoryMock.Setup(r => r.AddSongToPlaylistAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()))
            .ReturnsAsync(1);

        var request = new CreateSongRequest
        {
            Name = "Valid Song",
            AudioId = Guid.NewGuid().ToString(),
            ImageId = Guid.NewGuid().ToString(),
            Album = album.Id,
            AuthorIds = new List<Guid>()
        };

        _songRepositoryMock.Setup(r => r.ExistsByNameAndAlbumAsync(request.Name, request.Album, null))
            .ReturnsAsync(false);

        Song? capturedSong = null;
        _songRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Song>()))
            .Callback<Song>(s => capturedSong = s)
            .ReturnsAsync((Song s) => s);

        _songRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), false, true, false))
            .ReturnsAsync(() => 
            {
                if (capturedSong != null)
                {
                    capturedSong.SongAuthors = new List<SongAuthor>();
                }
                return capturedSong;
            });

        _userRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync(new List<User>());

        _fileServiceMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        _fileServiceMock.Setup(f => f.GetAudioDurationAsync(It.IsAny<string>())).ReturnsAsync(180);
        _fileServiceMock.Setup(f => f.MoveUploadedFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("file/path");

        var (song, error) = await _service.CreateSongAsync(request, isAdmin: true);

        Assert.NotNull(song);
        Assert.Null(error);
        Assert.Equal("Valid Song", song!.Name);
    }

    [Fact]
    public async Task UpdateSongAsync_UpdatesSong_WhenValid()
    {
        var song = new Song { Id = Guid.NewGuid(), Name = "Old Name", IsActive = true, SongAuthors = new List<SongAuthor>(), Album = Guid.NewGuid() };
        
        _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, true, true, false))
            .ReturnsAsync(song);

        Song? updatedSong = null;
        _songRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Song>()))
            .Callback<Song>(s => updatedSong = s)
            .Returns(Task.CompletedTask);

        _playlistRepositoryMock.Setup(r => r.GetByIdAsync(song.Album, true, false))
            .ReturnsAsync(new Playlist { Id = song.Album, Name = "Album", IsAlbum = true });
        _playlistRepositoryMock.Setup(r => r.GetMaxSongOrderAsync(song.Album))
            .ReturnsAsync(0);
        _playlistRepositoryMock.Setup(r => r.AddSongToPlaylistAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()))
            .ReturnsAsync(1);

        var request = new UpdateSongRequest
        {
            Name = "Updated Name"
        };

        var (success, error) = await _service.UpdateSongAsync(song.Id, request, isAdmin: true);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("Updated Name", updatedSong!.Name);
    }

    [Fact]
    public async Task DeleteSongAsync_DeletesSong_WhenExists()
    {
        var song = new Song
        {
            Id = Guid.NewGuid(),
            Name = "To Delete",
            AudioUrl = "audio.mp3",
            Picture = "image.png",
            IsActive = true
        };
        
        _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, true, true, false))
            .ReturnsAsync(song);

        _songRepositoryMock.Setup(r => r.DeleteAsync(song.Id))
            .Returns(Task.CompletedTask);

        _fileServiceMock.Setup(f => f.DeleteFileAsync("audio.mp3")).ReturnsAsync(true);
        _fileServiceMock.Setup(f => f.DeleteFileAsync("image.png")).ReturnsAsync(true);

        var result = await _service.DeleteSongAsync(song.Id, isAdmin: true);

        Assert.True(result);
        _songRepositoryMock.Verify(r => r.DeleteAsync(song.Id), Times.Once);
    }

    // Add these tests to your existing SongServiceTests class

[Fact]
public async Task CreateSongAsync_ReturnsError_WhenNonAdminAuthorCreatesForAnotherAuthor()
{
    var authorId = Guid.NewGuid();
    var differentAuthorId = Guid.NewGuid();
    
    var request = new CreateSongRequest
    {
        Name = "New Song",
        AudioId = Guid.NewGuid().ToString(),
        ImageId = Guid.NewGuid().ToString(),
        Album = Guid.NewGuid(),
        AuthorIds = new List<Guid> { differentAuthorId }
    };

    var (song, error) = await _service.CreateSongAsync(request, authorId, isAdmin: false);

    Assert.Null(song);
    Assert.Contains("Authors can only create songs for themselves", error!);
}

[Fact]
public async Task CreateSongAsync_ReturnsError_WhenAlbumNotFound()
{
    var albumId = Guid.NewGuid();
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(albumId, false, false))
        .ReturnsAsync((Playlist?)null);

    var request = new CreateSongRequest
    {
        Name = "New Song",
        AudioId = Guid.NewGuid().ToString(),
        ImageId = Guid.NewGuid().ToString(),
        Album = albumId,
        AuthorIds = new List<Guid>()
    };

    _fileServiceMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

    var (song, error) = await _service.CreateSongAsync(request, isAdmin: true);

    Assert.Null(song);
    Assert.Contains("not found or is not an album", error!);
}

[Fact]
public async Task CreateSongAsync_ReturnsError_WhenAlbumIsNotAnAlbum()
{
    var playlist = new Playlist { Id = Guid.NewGuid(), Name = "Playlist", IsAlbum = false };
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, false, false))
        .ReturnsAsync(playlist);

    var request = new CreateSongRequest
    {
        Name = "New Song",
        AudioId = Guid.NewGuid().ToString(),
        ImageId = Guid.NewGuid().ToString(),
        Album = playlist.Id,
        AuthorIds = new List<Guid>()
    };

    _fileServiceMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

    var (song, error) = await _service.CreateSongAsync(request, isAdmin: true);

    Assert.Null(song);
    Assert.Contains("not found or is not an album", error!);
}

[Fact]
public async Task CreateSongAsync_ReturnsError_WhenDuplicateSongNameInAlbum()
{
    var album = new Playlist { Id = Guid.NewGuid(), Name = "Album", IsAlbum = true };
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(album.Id, false, false))
        .ReturnsAsync(album);

    var request = new CreateSongRequest
    {
        Name = "Duplicate Song",
        AudioId = Guid.NewGuid().ToString(),
        ImageId = Guid.NewGuid().ToString(),
        Album = album.Id,
        AuthorIds = new List<Guid>()
    };

    _songRepositoryMock.Setup(r => r.ExistsByNameAndAlbumAsync(request.Name, request.Album, null))
        .ReturnsAsync(true);

    _fileServiceMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

    var (song, error) = await _service.CreateSongAsync(request, isAdmin: true);

    Assert.Null(song);
    Assert.Contains("already exists in this album", error!);
}

[Fact]
public async Task CreateSongAsync_ReturnsError_WhenInvalidAuthorIds()
{
    var album = new Playlist { Id = Guid.NewGuid(), Name = "Album", IsAlbum = true };
    var validAuthorId = Guid.NewGuid();
    var invalidAuthorId = Guid.NewGuid();
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(album.Id, false, false))
        .ReturnsAsync(album);

    var request = new CreateSongRequest
    {
        Name = "New Song",
        AudioId = Guid.NewGuid().ToString(),
        ImageId = Guid.NewGuid().ToString(),
        Album = album.Id,
        AuthorIds = new List<Guid> { validAuthorId, invalidAuthorId }
    };

    _songRepositoryMock.Setup(r => r.ExistsByNameAndAlbumAsync(request.Name, request.Album, null))
        .ReturnsAsync(false);

    _fileServiceMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

    // Only return the valid author
    _userRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>()))
        .ReturnsAsync(new List<User> 
        { 
            new User { Id = validAuthorId, Role = UserRole.Author, Name = "Valid Author" }
        });

    var (song, error) = await _service.CreateSongAsync(request, isAdmin: true);

    Assert.Null(song);
    Assert.Contains("do not exist or are not authors", error!);
}

[Fact]
public async Task CreateSongAsync_ReturnsError_WhenAudioDurationIsZero()
{
    var album = new Playlist { Id = Guid.NewGuid(), Name = "Album", IsAlbum = true };
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(album.Id, false, false))
        .ReturnsAsync(album);

    var request = new CreateSongRequest
    {
        Name = "New Song",
        AudioId = Guid.NewGuid().ToString(),
        ImageId = Guid.NewGuid().ToString(),
        Album = album.Id,
        AuthorIds = new List<Guid>()
    };

    _songRepositoryMock.Setup(r => r.ExistsByNameAndAlbumAsync(request.Name, request.Album, null))
        .ReturnsAsync(false);

    _fileServiceMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
    _fileServiceMock.Setup(f => f.GetAudioDurationAsync(It.IsAny<string>())).ReturnsAsync(0);

    _userRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>()))
        .ReturnsAsync(new List<User>());

    var (song, error) = await _service.CreateSongAsync(request, isAdmin: true);

    Assert.Null(song);
    Assert.Contains("Unable to determine audio file duration", error!);
}

[Fact]
public async Task CreateSongAsync_ReturnsError_WhenAudioDurationReadFails()
{
    var album = new Playlist { Id = Guid.NewGuid(), Name = "Album", IsAlbum = true };
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(album.Id, false, false))
        .ReturnsAsync(album);

    var request = new CreateSongRequest
    {
        Name = "New Song",
        AudioId = Guid.NewGuid().ToString(),
        ImageId = Guid.NewGuid().ToString(),
        Album = album.Id,
        AuthorIds = new List<Guid>()
    };

    _songRepositoryMock.Setup(r => r.ExistsByNameAndAlbumAsync(request.Name, request.Album, null))
        .ReturnsAsync(false);

    _fileServiceMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
    _fileServiceMock.Setup(f => f.GetAudioDurationAsync(It.IsAny<string>()))
        .ThrowsAsync(new Exception("Audio read error"));

    _userRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>()))
        .ReturnsAsync(new List<User>());

    var (song, error) = await _service.CreateSongAsync(request, isAdmin: true);

    Assert.Null(song);
    Assert.Contains("Failed to read audio file duration", error!);
}

[Fact]
public async Task CreateSongAsync_ReturnsError_WhenAudioFileMoveFails()
{
    var album = new Playlist { Id = Guid.NewGuid(), Name = "Album", IsAlbum = true };
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(album.Id, false, false))
        .ReturnsAsync(album);

    var request = new CreateSongRequest
    {
        Name = "New Song",
        AudioId = Guid.NewGuid().ToString(),
        ImageId = Guid.NewGuid().ToString(),
        Album = album.Id,
        AuthorIds = new List<Guid>()
    };

    _songRepositoryMock.Setup(r => r.ExistsByNameAndAlbumAsync(request.Name, request.Album, null))
        .ReturnsAsync(false);

    _fileServiceMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
    _fileServiceMock.Setup(f => f.GetAudioDurationAsync(It.IsAny<string>())).ReturnsAsync(180);
    _fileServiceMock.Setup(f => f.MoveUploadedFileAsync(request.AudioId, "audio", It.IsAny<string>()))
        .ThrowsAsync(new Exception("File move error"));

    _userRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>()))
        .ReturnsAsync(new List<User>());

    var (song, error) = await _service.CreateSongAsync(request, isAdmin: true);

    Assert.Null(song);
    Assert.Contains("Failed to process audio file", error!);
}

[Fact]
public async Task CreateSongAsync_ReturnsError_WhenImageFileMoveFails()
{
    var album = new Playlist { Id = Guid.NewGuid(), Name = "Album", IsAlbum = true };
    
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(album.Id, false, false))
        .ReturnsAsync(album);

    var request = new CreateSongRequest
    {
        Name = "New Song",
        AudioId = Guid.NewGuid().ToString(),
        ImageId = Guid.NewGuid().ToString(),
        Album = album.Id,
        AuthorIds = new List<Guid>()
    };

    _songRepositoryMock.Setup(r => r.ExistsByNameAndAlbumAsync(request.Name, request.Album, null))
        .ReturnsAsync(false);

    _fileServiceMock.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
    _fileServiceMock.Setup(f => f.GetAudioDurationAsync(It.IsAny<string>())).ReturnsAsync(180);
    _fileServiceMock.Setup(f => f.MoveUploadedFileAsync(request.AudioId, "audio", It.IsAny<string>()))
        .ReturnsAsync("audio/path");
    _fileServiceMock.Setup(f => f.MoveUploadedFileAsync(request.ImageId, "images", It.IsAny<string>()))
        .ThrowsAsync(new Exception("Image move error"));

    _userRepositoryMock.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>()))
        .ReturnsAsync(new List<User>());

    var (song, error) = await _service.CreateSongAsync(request, isAdmin: true);

    Assert.Null(song);
    Assert.Contains("Failed to process image file", error!);
}

[Fact]
public async Task UpdateSongAsync_ReturnsFailure_WhenSongNotFound()
{
    var songId = Guid.NewGuid();
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(songId, true, true, false))
        .ReturnsAsync((Song?)null);

    var request = new UpdateSongRequest { Name = "Updated Name" };

    var (success, error) = await _service.UpdateSongAsync(songId, request, isAdmin: true);

    Assert.False(success);
    Assert.Null(error);
}

[Fact]
public async Task UpdateSongAsync_ReturnsFailure_WhenNonAdminNonOwner()
{
    var song = new Song 
    { 
        Id = Guid.NewGuid(), 
        Name = "Song", 
        IsActive = true, 
        SongAuthors = new List<SongAuthor>
        {
            new SongAuthor { UserId = Guid.NewGuid() }
        },
        Album = Guid.NewGuid()
    };
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, true, true, false))
        .ReturnsAsync(song);

    var differentUserId = Guid.NewGuid();
    var request = new UpdateSongRequest { Name = "Updated Name" };

    var (success, error) = await _service.UpdateSongAsync(song.Id, request, differentUserId, isAdmin: false);

    Assert.False(success);
    Assert.Null(error);
}

[Fact]
public async Task UpdateSongAsync_ReturnsError_WhenAlbumNotFound()
{
    var song = new Song 
    { 
        Id = Guid.NewGuid(), 
        Name = "Song", 
        IsActive = true, 
        SongAuthors = new List<SongAuthor>(),
        Album = Guid.NewGuid()
    };
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, true, true, false))
        .ReturnsAsync(song);

    var newAlbumId = Guid.NewGuid();
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(newAlbumId, true, false))
        .ReturnsAsync((Playlist?)null);

    var request = new UpdateSongRequest { Album = newAlbumId };

    var (success, error) = await _service.UpdateSongAsync(song.Id, request, isAdmin: true);

    Assert.False(success);
    Assert.Contains("Invalid album ID", error!);
}

[Fact]
public async Task UpdateSongAsync_ReturnsError_WhenAlbumIsNotAnAlbum()
{
    var song = new Song 
    { 
        Id = Guid.NewGuid(), 
        Name = "Song", 
        IsActive = true, 
        SongAuthors = new List<SongAuthor>(),
        Album = Guid.NewGuid()
    };
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, true, true, false))
        .ReturnsAsync(song);

    var newAlbumId = Guid.NewGuid();
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(newAlbumId, true, false))
        .ReturnsAsync(new Playlist { Id = newAlbumId, IsAlbum = false });

    var request = new UpdateSongRequest { Album = newAlbumId };

    var (success, error) = await _service.UpdateSongAsync(song.Id, request, isAdmin: true);

    Assert.False(success);
    Assert.Contains("Invalid album ID", error!);
}

[Fact]
public async Task UpdateSongAsync_ReturnsError_WhenNonAdminTriesToChangeToUnownedAlbum()
{
    var userId = Guid.NewGuid();
    var song = new Song 
    { 
        Id = Guid.NewGuid(), 
        Name = "Song", 
        IsActive = true, 
        SongAuthors = new List<SongAuthor> 
        { 
            new SongAuthor { UserId = userId } 
        },
        Album = Guid.NewGuid()
    };
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, true, true, false))
        .ReturnsAsync(song);

    var newAlbumId = Guid.NewGuid();
    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(newAlbumId, true, false))
        .ReturnsAsync(new Playlist 
        { 
            Id = newAlbumId, 
            IsAlbum = true,
            PlaylistOwners = new List<PlaylistOwner>
            {
                new PlaylistOwner { UserId = Guid.NewGuid() } // Different user
            }
        });

    var request = new UpdateSongRequest { Album = newAlbumId };

    var (success, error) = await _service.UpdateSongAsync(song.Id, request, userId, isAdmin: false);

    Assert.False(success);
    Assert.Contains("you do not have permission", error!);
}

[Fact]
public async Task UpdateSongAsync_ChangesAlbum_WhenValid()
{
    var oldAlbumId = Guid.NewGuid();
    var newAlbumId = Guid.NewGuid();
    var song = new Song 
    { 
        Id = Guid.NewGuid(), 
        Name = "Song", 
        IsActive = true, 
        SongAuthors = new List<SongAuthor>(),
        Album = oldAlbumId
    };
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, true, true, false))
        .ReturnsAsync(song);

    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(newAlbumId, true, false))
        .ReturnsAsync(new Playlist { Id = newAlbumId, IsAlbum = true, PlaylistOwners = new List<PlaylistOwner>() });
    
    _playlistRepositoryMock.Setup(r => r.RemoveSongFromPlaylistAsync(oldAlbumId, song.Id))
        .Returns(Task.CompletedTask);
    
    _playlistRepositoryMock.Setup(r => r.GetMaxSongOrderAsync(newAlbumId))
        .ReturnsAsync(5);
    
    _playlistRepositoryMock.Setup(r => r.AddSongToPlaylistAsync(newAlbumId, song.Id, 6))
        .ReturnsAsync(1);

    _songRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Song>()))
        .Returns(Task.CompletedTask);

    var request = new UpdateSongRequest { Album = newAlbumId };

    var (success, error) = await _service.UpdateSongAsync(song.Id, request, isAdmin: true);

    Assert.True(success);
    Assert.Null(error);
    _playlistRepositoryMock.Verify(r => r.RemoveSongFromPlaylistAsync(oldAlbumId, song.Id), Times.Once);
    _playlistRepositoryMock.Verify(r => r.AddSongToPlaylistAsync(newAlbumId, song.Id, 6), Times.Once);
}

[Fact]
public async Task UpdateSongAsync_UpdatesAllFields_WhenProvided()
{
    var song = new Song 
    { 
        Id = Guid.NewGuid(), 
        Name = "Old Name",
        Description = "Old Desc",
        Genre = "Old Genre",
        Tags = "old,tags",
        ReleaseDate = DateTime.Now.AddYears(-1),
        IsActive = true, 
        SongAuthors = new List<SongAuthor>(),
        Album = Guid.NewGuid()
    };
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, true, true, false))
        .ReturnsAsync(song);

    _playlistRepositoryMock.Setup(r => r.GetByIdAsync(song.Album, true, false))
        .ReturnsAsync(new Playlist { Id = song.Album, IsAlbum = true });

    Song? updatedSong = null;
    _songRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Song>()))
        .Callback<Song>(s => updatedSong = s)
        .Returns(Task.CompletedTask);

    var newReleaseDate = DateTime.Now;
    var request = new UpdateSongRequest
    {
        Name = "New Name",
        Description = "New Description",
        Genre = "New Genre",
        Tags = new List<string> { "new", "tags" },
        ReleaseDate = newReleaseDate
    };

    var (success, error) = await _service.UpdateSongAsync(song.Id, request, isAdmin: true);

    Assert.True(success);
    Assert.Null(error);
    Assert.NotNull(updatedSong);
    Assert.Equal("New Name", updatedSong!.Name);
    Assert.Equal("New Description", updatedSong.Description);
    Assert.Equal("New Genre", updatedSong.Genre);
    Assert.Equal("new,tags", updatedSong.Tags);
    Assert.Equal(newReleaseDate, updatedSong.ReleaseDate);
}

[Fact]
public async Task DeleteSongAsync_ReturnsFalse_WhenSongNotFound()
{
    var songId = Guid.NewGuid();
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(songId, true, true, false))
        .ReturnsAsync((Song?)null);

    var result = await _service.DeleteSongAsync(songId, isAdmin: true);

    Assert.False(result);
    _songRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
}

[Fact]
public async Task DeleteSongAsync_ReturnsFalse_WhenNonAdminNonOwner()
{
    var song = new Song
    {
        Id = Guid.NewGuid(),
        Name = "Song",
        IsActive = true,
        SongAuthors = new List<SongAuthor>
        {
            new SongAuthor { UserId = Guid.NewGuid() }
        }
    };
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, true, true, false))
        .ReturnsAsync(song);

    var differentUserId = Guid.NewGuid();
    var result = await _service.DeleteSongAsync(song.Id, differentUserId, isAdmin: false);

    Assert.False(result);
    _songRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
}

[Fact]
public async Task DeleteSongAsync_DeletesSong_WhenUserIsOwner()
{
    var userId = Guid.NewGuid();
    var song = new Song
    {
        Id = Guid.NewGuid(),
        Name = "Song",
        AudioUrl = "audio.mp3",
        Picture = "image.png",
        IsActive = true,
        SongAuthors = new List<SongAuthor>
        {
            new SongAuthor { UserId = userId }
        }
    };
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, true, true, false))
        .ReturnsAsync(song);

    _songRepositoryMock.Setup(r => r.DeleteAsync(song.Id))
        .Returns(Task.CompletedTask);

    _fileServiceMock.Setup(f => f.DeleteFileAsync(It.IsAny<string>())).ReturnsAsync(true);

    var result = await _service.DeleteSongAsync(song.Id, userId, isAdmin: false);

    Assert.True(result);
    _songRepositoryMock.Verify(r => r.DeleteAsync(song.Id), Times.Once);
}

[Fact]
public async Task DeleteSongAsync_HandlesNullAudioUrl()
{
    var song = new Song
    {
        Id = Guid.NewGuid(),
        Name = "Song",
        AudioUrl = string.Empty,
        Picture = "image.png",
        IsActive = true,
        SongAuthors = new List<SongAuthor>()
    };
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, true, true, false))
        .ReturnsAsync(song);

    _songRepositoryMock.Setup(r => r.DeleteAsync(song.Id))
        .Returns(Task.CompletedTask);

    _fileServiceMock.Setup(f => f.DeleteFileAsync("image.png")).ReturnsAsync(true);

    var result = await _service.DeleteSongAsync(song.Id, isAdmin: true);

    Assert.True(result);
    _fileServiceMock.Verify(f => f.DeleteFileAsync("image.png"), Times.Once);
}

[Fact]
public async Task DeleteSongAsync_HandlesNullPicture()
{
    var song = new Song
    {
        Id = Guid.NewGuid(),
        Name = "Song",
        AudioUrl = "audio.mp3",
        Picture = string.Empty,
        IsActive = true,
        SongAuthors = new List<SongAuthor>()
    };
    
    _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, true, true, false))
        .ReturnsAsync(song);

    _songRepositoryMock.Setup(r => r.DeleteAsync(song.Id))
        .Returns(Task.CompletedTask);

    _fileServiceMock.Setup(f => f.DeleteFileAsync("audio.mp3")).ReturnsAsync(true);

    var result = await _service.DeleteSongAsync(song.Id, isAdmin: true);

    Assert.True(result);
    _fileServiceMock.Verify(f => f.DeleteFileAsync("audio.mp3"), Times.Once);
}

[Fact]
public async Task SearchSongsAsync_ReturnsEmptyList_WhenNoMatches()
{
    _songRepositoryMock.Setup(r => r.SearchAsync("NonexistentQuery"))
        .ReturnsAsync(new List<Song>());

    var results = await _service.SearchSongsAsync("NonexistentQuery");

    Assert.Empty(results);
}

[Fact]
public async Task SearchSongsAsync_ReturnsMultipleSongs_WhenMultipleMatches()
{
    var song1 = new Song 
    { 
        Id = Guid.NewGuid(), 
        Name = "Rock Song 1", 
        Genre = "Rock", 
        IsActive = true,
        SongAuthors = new List<SongAuthor>
        {
            new SongAuthor { User = new User { Id = Guid.NewGuid(), Name = "Author 1", Role = UserRole.Author } }
        }
    };
    var song2 = new Song 
    { 
        Id = Guid.NewGuid(), 
        Name = "Rock Song 2", 
        Genre = "Rock", 
        IsActive = true,
        SongAuthors = new List<SongAuthor>
        {
            new SongAuthor { User = new User { Id = Guid.NewGuid(), Name = "Author 2", Role = UserRole.Author } }
        }
    };
    
    _songRepositoryMock.Setup(r => r.SearchAsync("Rock"))
        .ReturnsAsync(new List<Song> { song1, song2 });

    var results = await _service.SearchSongsAsync("Rock");

    Assert.Equal(2, results.Count);
    Assert.Contains(results, r => r.Name == "Rock Song 1");
    Assert.Contains(results, r => r.Name == "Rock Song 2");
}

[Fact]
public async Task SearchSongsAsync_FiltersNonAuthorUsers()
{
    var song = new Song 
    { 
        Id = Guid.NewGuid(), 
        Name = "Test Song", 
        IsActive = true,
        SongAuthors = new List<SongAuthor>
        {
            new SongAuthor { User = new User { Id = Guid.NewGuid(), Name = "Author", Role = UserRole.Author } },
            new SongAuthor { User = new User { Id = Guid.NewGuid(), Name = "Admin", Role = UserRole.Admin } }
        }
    };
    
    _songRepositoryMock.Setup(r => r.SearchAsync("Test"))
        .ReturnsAsync(new List<Song> { song });

    var results = await _service.SearchSongsAsync("Test");

    Assert.Single(results);
    Assert.Single(results[0].AuthorNames);
    Assert.Equal("Author", results[0].AuthorNames[0]);
}


    [Fact]
    public async Task SearchSongsAsync_ReturnsMatchingSongs()
    {
        var song1 = new Song { Id = Guid.NewGuid(), Name = "Rock Song", Genre = "Rock", IsActive = true };
        var song2 = new Song { Id = Guid.NewGuid(), Name = "Jazz Song", Genre = "Jazz", IsActive = true };
        
        _songRepositoryMock.Setup(r => r.SearchAsync("Rock"))
            .ReturnsAsync(new List<Song> { song1 });

        var results = await _service.SearchSongsAsync("Rock");

        Assert.Single(results);
        Assert.Equal("Rock Song", results[0].Name);
    }
}