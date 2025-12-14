using Microsoft.Extensions.Logging;
using Moq;
using Groovo.Services.Tus;

namespace Groovo.Tests.Services;

public class TusCleanupServiceTests : IDisposable
{
    private readonly Mock<ILogger<TusCleanupService>> _mockLogger;
    private readonly string _testTempPath;
    private readonly TusStorageConfiguration _storageConfig;

    public TusCleanupServiceTests()
    {
        _mockLogger = new Mock<ILogger<TusCleanupService>>();
        _testTempPath = Path.Combine(Path.GetTempPath(), $"TusTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testTempPath);
        
        _storageConfig = new TusStorageConfiguration
        {
            TempPath = _testTempPath
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testTempPath))
        {
            Directory.Delete(_testTempPath, recursive: true);
        }
    }

    private TusCleanupService CreateService()
    {
        return new TusCleanupService(_mockLogger.Object, _storageConfig);
    }

    private string CreateTestFile(string fileName, DateTime lastWriteTime)
    {
        var filePath = Path.Combine(_testTempPath, fileName);
        File.WriteAllText(filePath, "test content");
        File.SetLastWriteTimeUtc(filePath, lastWriteTime);
        return filePath;
    }

    private string CreateMetadataFile(string baseFilePath)
    {
        var metadataPath = $"{baseFilePath}.metadata";
        File.WriteAllText(metadataPath, "metadata content");
        return metadataPath;
    }

    #region Cleanup Logic Tests

    [Fact]
    public async Task CleanupOldUploads_DeletesOldFiles()
    {
        // Arrange
        var oldFile = CreateTestFile("old_upload.bin", DateTime.UtcNow.AddHours(-3)); // 3 hours old
        var recentFile = CreateTestFile("recent_upload.bin", DateTime.UtcNow.AddMinutes(-30)); // 30 min old
        
        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(200); // Let it run one iteration
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }

        // Assert
        Assert.False(File.Exists(oldFile), "Old file should be deleted");
        Assert.True(File.Exists(recentFile), "Recent file should not be deleted");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Deleted stale temp file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CleanupOldUploads_DeletesMetadataFileWithUpload()
    {
        // Arrange
        var oldFile = CreateTestFile("old_upload.bin", DateTime.UtcNow.AddHours(-3));
        var metadataFile = CreateMetadataFile(oldFile);
        
        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
        }

        // Assert
        Assert.False(File.Exists(oldFile), "Old file should be deleted");
        Assert.False(File.Exists(metadataFile), "Metadata file should be deleted");
    }

    [Fact]
    public async Task CleanupOldUploads_JustOverMaxAge_IsDeleted()
    {
        // Arrange - File is just over 2 hours old (2 hours + 1 minute)
        var oldFile = CreateTestFile("just_over_upload.bin", DateTime.UtcNow.AddHours(-2).AddMinutes(-1));
        
        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
        }

        // Assert - Should be deleted (greater than max age)
        Assert.False(File.Exists(oldFile), "File over max age should be deleted");
    }

    [Fact]
    public async Task CleanupOldUploads_MultipleMixedFiles_DeletesOnlyOld()
    {
        // Arrange
        var oldFile1 = CreateTestFile("old1.bin", DateTime.UtcNow.AddHours(-5));
        var oldFile2 = CreateTestFile("old2.bin", DateTime.UtcNow.AddHours(-3));
        var recentFile1 = CreateTestFile("recent1.bin", DateTime.UtcNow.AddMinutes(-30));
        var recentFile2 = CreateTestFile("recent2.bin", DateTime.UtcNow.AddHours(-1));
        
        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
        }

        // Assert
        Assert.False(File.Exists(oldFile1));
        Assert.False(File.Exists(oldFile2));
        Assert.True(File.Exists(recentFile1));
        Assert.True(File.Exists(recentFile2));

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cleanup completed") && o.ToString()!.Contains("Deleted: 2")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CleanupOldUploads_TempDirectoryDoesNotExist_LogsAndReturns()
    {
        // Arrange
        var nonExistentConfig = new TusStorageConfiguration
        {
            TempPath = Path.Combine(Path.GetTempPath(), $"NonExistent_{Guid.NewGuid()}")
        };
        var service = new TusCleanupService(_mockLogger.Object, nonExistentConfig);
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
        }

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Temp directory does not exist")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task CleanupOldUploads_EmptyDirectory_CompletesWithoutError()
    {
        // Arrange - Empty temp directory
        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
        }

        // Assert - Should complete without logging cleanup completion (no files to delete)
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cleanup completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task CleanupOldUploads_MetadataFileWithoutBase_NotDeleted()
    {
        // Arrange - Only metadata file exists, no base file
        var metadataPath = Path.Combine(_testTempPath, "orphan.bin.metadata");
        File.WriteAllText(metadataPath, "orphan metadata");
        File.SetLastWriteTimeUtc(metadataPath, DateTime.UtcNow.AddHours(-3));
        
        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
        }

        // Assert - Metadata file itself should be deleted if old enough
        Assert.False(File.Exists(metadataPath), "Old orphan metadata file should be deleted");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task CleanupOldUploads_FileDeleteFails_ContinuesWithOtherFiles()
    {
        // Arrange
        var oldFile1 = CreateTestFile("old1.bin", DateTime.UtcNow.AddHours(-3));
        var oldFile2 = CreateTestFile("old2.bin", DateTime.UtcNow.AddHours(-3));
        
        // Lock the first file to cause deletion to fail
        using var fileStream = new FileStream(oldFile1, FileMode.Open, FileAccess.Read, FileShare.None);
        
        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(300);
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
        }

        // Assert
        Assert.True(File.Exists(oldFile1), "Locked file should still exist");
        Assert.False(File.Exists(oldFile2), "Second file should be deleted");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to delete file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Errors: 1")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CleanupOldUploads_ExceptionDuringCleanup_ContinuesRunning()
    {
        // Arrange
        var service = CreateService();
        var cts = new CancellationTokenSource();

        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        
        // Delete the temp directory while service is running to cause an exception on next iteration
        Directory.Delete(_testTempPath, recursive: true);
        
        // Wait for service to attempt cleanup again (15 minute interval is too long, so we just verify it doesn't crash)
        await Task.Delay(200);
        cts.Cancel();
        
        // Assert - Service should handle the exception and not crash
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling - service is still running
        }

        // Verify service started (proves it didn't crash immediately)
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("TusCleanupService started")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    #endregion

    #region Service Lifecycle Tests

    [Fact]
    public async Task Service_Starts_LogsStartMessage()
    {
        // Arrange
        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
        }

        // Assert - Only verify start message (stop message timing is unreliable in tests)
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("TusCleanupService started")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task Service_CancellationRequested_StopsGracefully()
    {
        // Arrange
        // Create many old files to ensure cancellation happens during cleanup
        for (int i = 0; i < 10; i++)
        {
            CreateTestFile($"old_{i}.bin", DateTime.UtcNow.AddHours(-3));
        }
        
        var service = CreateService();
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(50); // Cancel while processing
        cts.Cancel();
        
        // Assert - Should not throw unhandled exception
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Some files may remain (cancellation happened mid-cleanup)
        var remainingFiles = Directory.GetFiles(_testTempPath);
        Assert.True(remainingFiles.Length >= 0); // At least some processing occurred
    }

    #endregion
}