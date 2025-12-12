using Microsoft.Extensions.Logging;
using Moq;
using Groovo.Models;
using Groovo.DTOs.Requests;
using Groovo.Services;
using Groovo.Repositories;

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
        var song = new Song { Id = Guid.NewGuid(), Name = "Test Song", IsActive = true };
        
        _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, false, false, false))
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

        var request = new CreateSongRequest
        {
            Name = "Valid Song",
            AudioId = Guid.NewGuid().ToString(),
            ImageId = Guid.NewGuid().ToString(),
            Album = album.Id,
            AuthorIds = new List<Guid>()
        };

        Song? capturedSong = null;
        _songRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<Song>()))
            .Callback<Song>(s => capturedSong = s)
            .ReturnsAsync((Song s) => s);

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
        var song = new Song { Id = Guid.NewGuid(), Name = "Old Name", IsActive = true };
        
        _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, false, false, false))
            .ReturnsAsync(song);

        Song? updatedSong = null;
        _songRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Song>()))
            .Callback<Song>(s => updatedSong = s)
            .Returns(Task.CompletedTask);

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
        
        _songRepositoryMock.Setup(r => r.GetByIdAsync(song.Id, false, false, false))
            .ReturnsAsync(song);

        _songRepositoryMock.Setup(r => r.DeleteAsync(song.Id))
            .Returns(Task.CompletedTask);

        _fileServiceMock.Setup(f => f.DeleteFileAsync("audio.mp3")).ReturnsAsync(true);
        _fileServiceMock.Setup(f => f.DeleteFileAsync("image.png")).ReturnsAsync(true);

        var result = await _service.DeleteSongAsync(song.Id, isAdmin: true);

        Assert.True(result);
        _songRepositoryMock.Verify(r => r.DeleteAsync(song.Id), Times.Once);
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