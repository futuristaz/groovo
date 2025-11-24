namespace Groovo.Services.Tus;

public class TusCleanupService : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _fileMaxAge = TimeSpan.FromHours(2);
    private readonly ILogger<TusCleanupService> _logger;
    private readonly TusStorageConfiguration _storageConfig;

    public TusCleanupService(ILogger<TusCleanupService> logger, TusStorageConfiguration storageConfig)
    {
        _logger = logger;
        _storageConfig = storageConfig;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TusCleanupService started. Running every {Interval} minutes", _cleanupInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldUploadsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during cleanup operation");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("TusCleanupService stopped");
    }

    private async Task CleanupOldUploadsAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_storageConfig.TempPath))
        {
            _logger.LogDebug("Temp directory does not exist: {TempPath}", _storageConfig.TempPath);
            return;
        }

        var now = DateTime.UtcNow;
        var deletedCount = 0;
        var errorCount = 0;

        try
        {
            var files = Directory.GetFiles(_storageConfig.TempPath, "*", SearchOption.TopDirectoryOnly);

            foreach (var filePath in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var fileInfo = new FileInfo(filePath);

                    // Check if file is older than max age
                    var fileAge = now - fileInfo.LastWriteTimeUtc;
                    if (fileAge > _fileMaxAge)
                    {
                        File.Delete(filePath);
                        deletedCount++;

                        _logger.LogInformation(
                            "Deleted stale temp file: {FileName}, Age: {Age} hours",
                            fileInfo.Name,
                            fileAge.TotalHours
                        );

                        // Also delete associated metadata file if exists
                        var metadataPath = $"{filePath}.metadata";
                        if (File.Exists(metadataPath))
                        {
                            File.Delete(metadataPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogWarning(ex, "Failed to delete file: {FilePath}", filePath);
                }

                await Task.Delay(10, cancellationToken);
            }

            if (deletedCount > 0 || errorCount > 0)
            {
                _logger.LogInformation(
                    "Cleanup completed. Deleted: {DeletedCount}, Errors: {ErrorCount}",
                    deletedCount,
                    errorCount
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup directory scan");
        }
    }
}
