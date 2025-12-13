using Moq;
using Microsoft.Extensions.Logging;
using Groovo.Services;
using Groovo.Services.Tus;
using System.Text;

namespace Groovo.Tests.Services;

public class TestTusStorageConfiguration : TusStorageConfiguration
{
    public TestTusStorageConfiguration(string tempPath, string finalPath)
    {
        typeof(TusStorageConfiguration).GetProperty("TempPath")!.SetValue(this, tempPath);
        typeof(TusStorageConfiguration).GetProperty("FinalPath")!.SetValue(this, finalPath);
    }
}

public class SongFileServiceTests : IDisposable
{
    private readonly SongFileService _service;
    private readonly TusStorageConfiguration _config;
    private readonly Mock<ILogger<SongFileService>> _loggerMock;
    private readonly string _testTempPath;
    private readonly string _testFinalPath;

    public SongFileServiceTests()
    {
        _loggerMock = new Mock<ILogger<SongFileService>>();
        
        // Use isolated test directories with unique GUID to avoid conflicts and protect real data
        var testId = Guid.NewGuid().ToString("N");
        _testTempPath = Path.Combine(Path.GetTempPath(), "GroovoTests", testId, "temp");
        _testFinalPath = Path.Combine(Path.GetTempPath(), "GroovoTests", testId, "final");
        
        // Use test-specific configuration
        _config = new TestTusStorageConfiguration(_testTempPath, _testFinalPath);
        
        _service = new SongFileService(_loggerMock.Object, _config);

        // Ensure clean test directories
        Directory.CreateDirectory(_config.TempPath);
        Directory.CreateDirectory(_config.FinalPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempPath))
                Directory.Delete(_testTempPath, true);

            if (Directory.Exists(_testFinalPath))
                Directory.Delete(_testFinalPath, true);
                
            var testParent = Path.Combine(Path.GetTempPath(), "GroovoTests");
            if (Directory.Exists(testParent) && Directory.GetFileSystemEntries(testParent).Length == 0)
                Directory.Delete(testParent, false);
        }
        catch { /* ignore */ }
    }

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

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

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


    [Fact]
    public void BuildFinalFileName_GeneratesValidName()
    {
        var finalName = _service.BuildFinalFileName("abc123", "song.mp3");

        Assert.Equal("abc123", finalName);
    }


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


    [Fact(Skip = "Requires real audio file in temp upload.")]
    public void GetAudioDurationAsync_WorksWithRealAudioFile()
    {
        
    }
}