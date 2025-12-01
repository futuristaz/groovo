using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Groovo.Services;
using Groovo.Services.Tus;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System;

namespace Groovo.Tests.Services;

public class SongFileServiceTests : IDisposable
{
    private readonly SongFileService _service;
    private readonly TusStorageConfiguration _config;
    private readonly Mock<ILogger<SongFileService>> _loggerMock;

    public SongFileServiceTests()
    {
        _loggerMock = new Mock<ILogger<SongFileService>>();
        _config = new TusStorageConfiguration(); // uses real hard-coded paths
        _service = new SongFileService(_loggerMock.Object, _config);

        // Ensure clean test directories
        Directory.CreateDirectory(_config.TempPath);
        Directory.CreateDirectory(_config.FinalPath);
    }

    // Clean up after tests
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_config.TempPath))
                Directory.Delete(_config.TempPath, true);

            if (Directory.Exists(_config.FinalPath))
                Directory.Delete(_config.FinalPath, true);
        }
        catch { /* ignore */ }
    }

    // ───────────────────────────────────────────────────────────────
    // MOVE FILE TESTS
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveUploadedFileAsync_MovesFileCorrectly()
    {
        var uploadId = "test123";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        // Create metadata with base64 filename "track.mp3"
        var metadataPath = tempFilePath + ".metadata";
        var filenameEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("track.mp3"));
        await File.WriteAllTextAsync(metadataPath, $"filename {filenameEncoded}");

        // Act
        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        // Assert
        Assert.Contains("songs", result);
        Assert.Contains(uploadId, result);

        var finalFilePath = Path.Combine(_config.FinalPath, result.Replace("/", "\\"));
        Assert.True(File.Exists(finalFilePath));
        Assert.False(File.Exists(tempFilePath)); // moved
        Assert.False(File.Exists(metadataPath)); // metadata deleted
    }

    [Fact]
    public async Task MoveUploadedFileAsync_Throws_WhenTempFileMissing()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.MoveUploadedFileAsync("missingId", "folder", "target"));
    }

    // ───────────────────────────────────────────────────────────────
    // FILE EXISTS TESTS
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task FileExistsAsync_ReturnsTrue_WhenFileExists()
    {
        var id = "existsFile";
        var tempFilePath = Path.Combine(_config.TempPath, id);
        await File.WriteAllTextAsync(tempFilePath, "x");

        var result = await _service.FileExistsAsync(id);

        Assert.True(result);
    }

    [Fact]
    public async Task FileExistsAsync_ReturnsFalse_WhenMissing()
    {
        var result = await _service.FileExistsAsync("nope");

        Assert.False(result);
    }

    // ───────────────────────────────────────────────────────────────
    // BUILD FINAL FILE NAME TEST
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void BuildFinalFileName_GeneratesValidName()
    {
        var finalName = _service.BuildFinalFileName("abc123", "song.mp3");

        Assert.Equal("abc123", finalName);
    }

    // ───────────────────────────────────────────────────────────────
    // DELETE FILE TESTS
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteFileAsync_DeletesFile()
    {
        var rel = "music/test.mp3";
        var full = Path.Combine(_config.FinalPath, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, "x");

        var result = await _service.DeleteFileAsync(rel);

        Assert.True(result);
        Assert.False(File.Exists(full));
    }

    [Fact]
    public async Task DeleteFileAsync_ReturnsFalse_WhenMissing()
    {
        var result = await _service.DeleteFileAsync("nope.mp3");
        Assert.False(result);
    }

    // ───────────────────────────────────────────────────────────────
    // AUDIO DURATION TEST (optional)
    // ───────────────────────────────────────────────────────────────

    [Fact(Skip = "Requires real audio file in temp upload.")]
    public void GetAudioDurationAsync_WorksWithRealAudioFile()
    {
        // To enable: copy small WAV/MP3 to _config.TempPath using uploadId
    }
}