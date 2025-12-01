using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Groovo.Data.Contexts;
using Groovo.Models;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Services;

namespace Groovo.Tests.Services;

public class SongServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<SongService>> _loggerMock;
    private readonly Mock<ISongFileService> _fileServiceMock;
    private readonly SongService _service;

    public SongServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<SongService>>();
        _fileServiceMock = new Mock<ISongFileService>();

        _service = new SongService(_context, _loggerMock.Object, _fileServiceMock.Object);
    }

    [Fact]
    public async Task GetSongByIdAsync_ReturnsSong_WhenSongExists()
    {
        var song = new Song { Id = Guid.NewGuid(), Name = "Test Song", IsActive = true };
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        var result = await _service.GetSongByIdAsync(song.Id);

        Assert.NotNull(result);
        Assert.Equal("Test Song", result!.Name);
    }

    [Fact]
    public async Task GetSongByIdAsync_ReturnsNull_WhenSongDoesNotExist()
    {
        var result = await _service.GetSongByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateSongAsync_ReturnsError_WhenAudioFileMissing()
    {
        var album = new Playlist { Id = Guid.NewGuid(), Name = "Album", IsAlbum = true };
        _context.Playlists.Add(album);
        await _context.SaveChangesAsync();

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
        _context.Playlists.Add(album);
        await _context.SaveChangesAsync();

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
        _context.Playlists.Add(album);
        await _context.SaveChangesAsync();

        var request = new CreateSongRequest
        {
            Name = "Valid Song",
            AudioId = Guid.NewGuid().ToString(),
            ImageId = Guid.NewGuid().ToString(),
            Album = album.Id,
            AuthorIds = new List<Guid>()
        };

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
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        var request = new UpdateSongRequest
        {
            Name = "Updated Name"
        };

        var (success, error) = await _service.UpdateSongAsync(song.Id, request, isAdmin: true);

        Assert.True(success);
        Assert.Null(error);

        var updatedSong = await _context.Songs.FindAsync(song.Id);
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
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        _fileServiceMock.Setup(f => f.DeleteFileAsync("audio.mp3")).ReturnsAsync(true);
        _fileServiceMock.Setup(f => f.DeleteFileAsync("image.png")).ReturnsAsync(true);

        var result = await _service.DeleteSongAsync(song.Id, isAdmin: true);

        Assert.True(result);
        var deleted = await _context.Songs.FindAsync(song.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task SearchSongsAsync_ReturnsMatchingSongs()
    {
        var song1 = new Song { Id = Guid.NewGuid(), Name = "Rock Song", Genre = "Rock", IsActive = true };
        var song2 = new Song { Id = Guid.NewGuid(), Name = "Jazz Song", Genre = "Jazz", IsActive = true };
        _context.Songs.AddRange(song1, song2);
        await _context.SaveChangesAsync();

        var results = await _service.SearchSongsAsync("Rock");

        Assert.Single(results);
        Assert.Equal("Rock Song", results[0].Name);
    }
}