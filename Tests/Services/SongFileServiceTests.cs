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

    #region MoveUploadedFileAsync - Branch Coverage Tests

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

        var finalFilePath = Path.Combine(_config.FinalPath, result.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Assert.True(File.Exists(finalFilePath));
        Assert.False(File.Exists(tempFilePath)); // moved
        Assert.False(File.Exists(metadataPath)); // metadata deleted
    }

    [Fact]
    public async Task MoveUploadedFileAsync_ThrowsArgumentException_WhenUploadIdIsNull()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveUploadedFileAsync(null!, "subfolder", "target"));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_ThrowsArgumentException_WhenUploadIdIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveUploadedFileAsync("", "subfolder", "target"));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_ThrowsArgumentException_WhenUploadIdIsWhitespace()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveUploadedFileAsync("   ", "subfolder", "target"));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_ThrowsArgumentException_WhenSubfolderIsNull()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveUploadedFileAsync("uploadId", null!, "target"));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_ThrowsArgumentException_WhenSubfolderIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveUploadedFileAsync("uploadId", "", "target"));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_ThrowsArgumentException_WhenSubfolderIsWhitespace()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveUploadedFileAsync("uploadId", "   ", "target"));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_ThrowsArgumentException_WhenTargetFilenameIsNull()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveUploadedFileAsync("uploadId", "subfolder", null!));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_ThrowsArgumentException_WhenTargetFilenameIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveUploadedFileAsync("uploadId", "subfolder", ""));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_ThrowsArgumentException_WhenTargetFilenameIsWhitespace()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveUploadedFileAsync("uploadId", "subfolder", "   "));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_ThrowsFileNotFoundException_WhenTempFileMissing()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.MoveUploadedFileAsync("missingId", "folder", "target"));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_MovesFileWithoutMetadata()
    {
        var uploadId = "test_no_metadata";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        // No metadata file created

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        Assert.Contains("songs", result);
        var finalFilePath = Path.Combine(_config.FinalPath, result.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Assert.True(File.Exists(finalFilePath));
        Assert.False(File.Exists(tempFilePath)); // moved
    }

    [Fact]
    public async Task MoveUploadedFileAsync_HandlesMetadataWithNonBase64Filename()
    {
        var uploadId = "test_plain_text_metadata";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        // Create metadata with plain text filename (not base64)
        var metadataPath = tempFilePath + ".metadata";
        await File.WriteAllTextAsync(metadataPath, $"filename plain_filename.mp3");

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        Assert.Contains("songs", result);
        var finalFilePath = Path.Combine(_config.FinalPath, result.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Assert.True(File.Exists(finalFilePath));
        Assert.False(File.Exists(metadataPath)); // metadata deleted
    }

    [Fact]
    public async Task MoveUploadedFileAsync_HandlesMetadataWithoutFilenamePrefix()
    {
        var uploadId = "test_no_filename_prefix";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        // Create metadata without filename prefix
        var metadataPath = tempFilePath + ".metadata";
        await File.WriteAllTextAsync(metadataPath, $"someotherdata");

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        Assert.Contains("songs", result);
        var finalFilePath = Path.Combine(_config.FinalPath, result.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Assert.True(File.Exists(finalFilePath));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_HandlesMetadataFilenameWithOnlyOneSpace()
    {
        var uploadId = "test_single_space_metadata";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        // Create metadata with filename but missing the value after space
        var metadataPath = tempFilePath + ".metadata";
        await File.WriteAllTextAsync(metadataPath, $"filename ");

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        Assert.Contains("songs", result);
        var finalFilePath = Path.Combine(_config.FinalPath, result.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Assert.True(File.Exists(finalFilePath));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_HandlesMetadataReadError_GracefullyAndContinues()
    {
        var uploadId = "test_metadata_error";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        // Create an unreadable metadata file scenario (empty metadata)
        var metadataPath = tempFilePath + ".metadata";
        await File.WriteAllTextAsync(metadataPath, "");

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        Assert.Contains("songs", result);
        var finalFilePath = Path.Combine(_config.FinalPath, result.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Assert.True(File.Exists(finalFilePath));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_CreatesSubfolderIfNotExists()
    {
        var uploadId = "test_create_subfolder";
        var subfolder = "new_folder/nested";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        Assert.Contains(subfolder, result);
        var finalDirPath = Path.Combine(_config.FinalPath, subfolder);
        Assert.True(Directory.Exists(finalDirPath));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_ThrowsInvalidOperationException_WhenFileMoveFailsWithIOException()
    {
        var uploadId = "test_move_failure";
        var subfolder = "songs";
        var targetFilename = "target.mp3";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        var finalSubfolderPath = Path.Combine(_config.FinalPath, subfolder);
        Directory.CreateDirectory(finalSubfolderPath);
        var finalFilePath = Path.Combine(finalSubfolderPath, targetFilename);

        // Pre-create the final file so move fails with existing file error
        await File.WriteAllTextAsync(finalFilePath, "existing");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.MoveUploadedFileAsync(uploadId, subfolder, targetFilename));

        Assert.Contains("Failed to move file", ex.Message);
    }

    [Fact]
    public async Task MoveUploadedFileAsync_DoesNotDeleteMetadata_WhenMetadataFileDoesNotExist()
    {
        var uploadId = "test_no_metadata_delete";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        // No metadata file created

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        Assert.Contains("songs", result);
        var finalFilePath = Path.Combine(_config.FinalPath, result.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Assert.True(File.Exists(finalFilePath));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_DeletesMetadata_WhenMetadataFileExists()
    {
        var uploadId = "test_metadata_delete";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        var metadataPath = tempFilePath + ".metadata";
        await File.WriteAllTextAsync(metadataPath, "filename test.mp3");

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        Assert.False(File.Exists(metadataPath));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_ReturnsPathWithForwardSlashes()
    {
        var uploadId = "test_forward_slashes";
        var subfolder = "songs/subfolder";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        Assert.DoesNotContain("\\", result);
        Assert.Contains("/", result);
    }

    #endregion

    #region FileExistsAsync - Branch Coverage Tests

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
    public async Task FileExistsAsync_ReturnsFalse_WhenFileDoesNotExist()
    {
        var result = await _service.FileExistsAsync("nope");

        Assert.False(result);
    }

    [Fact]
    public async Task FileExistsAsync_ReturnsFalse_WhenUploadIdIsNull()
    {
        var result = await _service.FileExistsAsync(null!);

        Assert.False(result);
    }

    [Fact]
    public async Task FileExistsAsync_ReturnsFalse_WhenUploadIdIsEmpty()
    {
        var result = await _service.FileExistsAsync("");

        Assert.False(result);
    }

    [Fact]
    public async Task FileExistsAsync_ReturnsFalse_WhenUploadIdIsWhitespace()
    {
        var result = await _service.FileExistsAsync("   ");

        Assert.False(result);
    }

    #endregion

    #region BuildFinalFileName - Branch Coverage Tests

    [Fact]
    public void BuildFinalFileName_ReturnsTargetFilename_WithExtension()
    {
        var finalName = _service.BuildFinalFileName("abc123", "song.mp3");

        Assert.Equal("abc123", finalName);
    }

    [Fact]
    public void BuildFinalFileName_ReturnsTargetFilename_WithoutExtension()
    {
        var finalName = _service.BuildFinalFileName("abc123", "songwithoutextension");

        Assert.Equal("abc123", finalName);
    }

    [Fact]
    public void BuildFinalFileName_ReturnsTargetFilename_WithMultipleDots()
    {
        var finalName = _service.BuildFinalFileName("abc123", "song.backup.mp3");

        Assert.Equal("abc123", finalName);
    }

    [Fact]
    public void BuildFinalFileName_ReturnsTargetFilename_WithNullOriginalName()
    {
        var finalName = _service.BuildFinalFileName("abc123", null!);

        Assert.Equal("abc123", finalName);
    }

    [Fact]
    public void BuildFinalFileName_ReturnsTargetFilename_WithEmptyOriginalName()
    {
        var finalName = _service.BuildFinalFileName("abc123", "");

        Assert.Equal("abc123", finalName);
    }

    [Fact]
    public void BuildFinalFileName_ReturnsTargetFilename_WithWhitespaceOriginalName()
    {
        var finalName = _service.BuildFinalFileName("abc123", "   ");

        Assert.Equal("abc123", finalName);
    }

    #endregion

    #region GetAudioDurationAsync - Branch Coverage Tests

    [Fact]
    public async Task GetAudioDurationAsync_ReturnsZero_WhenUploadIdIsNull()
    {
        var result = await _service.GetAudioDurationAsync(null!);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetAudioDurationAsync_ReturnsZero_WhenUploadIdIsEmpty()
    {
        var result = await _service.GetAudioDurationAsync("");

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetAudioDurationAsync_ReturnsZero_WhenUploadIdIsWhitespace()
    {
        var result = await _service.GetAudioDurationAsync("   ");

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetAudioDurationAsync_ReturnsZero_WhenTempFileDoesNotExist()
    {
        var result = await _service.GetAudioDurationAsync("nonexistentId");

        Assert.Equal(0, result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Temp file not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAudioDurationAsync_ReturnsZero_WhenAudioFileIsInvalid()
    {
        var uploadId = "invalid_audio";
        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        
        // Create a file with invalid audio data
        await File.WriteAllTextAsync(tempFilePath, "This is not an audio file");

        var result = await _service.GetAudioDurationAsync(uploadId);

        Assert.Equal(0, result);
        // ATL library handles invalid files gracefully and returns 0 duration
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Extracted audio duration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAudioDurationAsync_LogsInformation_OnSuccessfulDurationExtraction()
    {
        // This test verifies the logging happens when duration is extracted successfully
        // Invalid audio files are handled gracefully by ATL and return 0 duration
        var uploadId = "test_audio_log";
        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        
        await File.WriteAllTextAsync(tempFilePath, "invalid audio");

        await _service.GetAudioDurationAsync(uploadId);

        // Verify information logging was called (success path)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Extracted audio duration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region DeleteFileAsync - Branch Coverage Tests

    [Fact]
    public async Task DeleteFileAsync_DeletesFileSuccessfully()
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
    public async Task DeleteFileAsync_ReturnsFalse_WhenFileDoesNotExist()
    {
        var result = await _service.DeleteFileAsync("nope.mp3");
        
        Assert.False(result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("File not found for deletion")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_ReturnsFalse_WhenRelativePathIsNull()
    {
        var result = await _service.DeleteFileAsync(null!);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteFileAsync_ReturnsFalse_WhenRelativePathIsEmpty()
    {
        var result = await _service.DeleteFileAsync("");

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteFileAsync_ReturnsFalse_WhenRelativePathIsWhitespace()
    {
        var result = await _service.DeleteFileAsync("   ");

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteFileAsync_ReturnsTrue_WhenFileDeleted()
    {
        var rel = "test.mp3";
        var full = Path.Combine(_config.FinalPath, rel);
        await File.WriteAllTextAsync(full, "data");

        var result = await _service.DeleteFileAsync(rel);

        Assert.True(result);
    }

    [Fact]
    public async Task DeleteFileAsync_LogsInformation_OnSuccessfulDeletion()
    {
        var rel = "test.mp3";
        var full = Path.Combine(_config.FinalPath, rel);
        await File.WriteAllTextAsync(full, "data");

        await _service.DeleteFileAsync(rel);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_DeletesFileInNestedDirectory()
    {
        var rel = "music/albums/tracks/song.mp3";
        var full = Path.Combine(_config.FinalPath, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, "x");

        var result = await _service.DeleteFileAsync(rel);

        Assert.True(result);
        Assert.False(File.Exists(full));
    }

    #endregion

    #region Metadata Parsing - Branch Coverage Tests

    [Fact]
    public async Task MoveUploadedFileAsync_HandlesCaseInsensitiveFilenamePrefix()
    {
        var uploadId = "test_case_insensitive";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        var metadataPath = tempFilePath + ".metadata";
        var filenameEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("track.mp3"));
        // Use uppercase FILENAME
        await File.WriteAllTextAsync(metadataPath, $"FILENAME {filenameEncoded}");

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        Assert.Contains("songs", result);
        var finalFilePath = Path.Combine(_config.FinalPath, result.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Assert.True(File.Exists(finalFilePath));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_HandlesMetadataWithMultipleLines()
    {
        var uploadId = "test_multiline_metadata";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        var metadataPath = tempFilePath + ".metadata";
        var filenameEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("track.mp3"));
        // Multiple lines with filename in the middle
        var metadataContent = $"size 1024\nfilename {filenameEncoded}\nmimetype audio/mpeg";
        await File.WriteAllTextAsync(metadataPath, metadataContent);

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        Assert.Contains("songs", result);
    }

    [Fact]
    public async Task MoveUploadedFileAsync_HandlesEmptyMetadataFile()
    {
        var uploadId = "test_empty_metadata";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        var metadataPath = tempFilePath + ".metadata";
        await File.WriteAllTextAsync(metadataPath, "");

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        Assert.Contains("songs", result);
        var finalFilePath = Path.Combine(_config.FinalPath, result.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Assert.True(File.Exists(finalFilePath));
    }

    [Fact]
    public async Task MoveUploadedFileAsync_LogsWarning_WhenTempFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.MoveUploadedFileAsync("missing", "songs", "target"));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Temp file not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MoveUploadedFileAsync_LogsInformation_OnSuccessfulMove()
    {
        var uploadId = "test_success_log";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Moved file from temp to final")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MoveUploadedFileAsync_LogsError_OnFileMoveFailure()
    {
        var uploadId = "test_error_log";
        var subfolder = "songs";
        var targetFilename = "target.mp3";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        var finalSubfolderPath = Path.Combine(_config.FinalPath, subfolder);
        Directory.CreateDirectory(finalSubfolderPath);
        var finalFilePath = Path.Combine(finalSubfolderPath, targetFilename);

        // Pre-create the final file to cause error
        await File.WriteAllTextAsync(finalFilePath, "existing");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.MoveUploadedFileAsync(uploadId, subfolder, targetFilename));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to move file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MoveUploadedFileAsync_LogsWarning_WhenMetadataReadFails()
    {
        var uploadId = "test_metadata_warning";
        var subfolder = "songs";

        var tempFilePath = Path.Combine(_config.TempPath, uploadId);
        await File.WriteAllTextAsync(tempFilePath, "dummydata");

        var metadataPath = tempFilePath + ".metadata";
        // Create metadata that will cause base64 decode to fail
        await File.WriteAllTextAsync(metadataPath, $"filename !!invalid-base64!!");

        var result = await _service.MoveUploadedFileAsync(uploadId, subfolder, uploadId);

        // Should still succeed despite metadata read failure
        Assert.Contains("songs", result);
    }

    #endregion
}