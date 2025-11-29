using Groovo.Services.Tus;
using ATL;

namespace Groovo.Services;

public class SongFileService : ISongFileService
{
    private readonly ILogger<SongFileService> _logger;
    private readonly TusStorageConfiguration _storageConfig;

    public SongFileService(ILogger<SongFileService> logger, TusStorageConfiguration storageConfig)
    {
        _logger = logger;
        _storageConfig = storageConfig;
    }

    public async Task<string> MoveUploadedFileAsync(string uploadId, string subfolder, string targetFilename)
    {
        if (string.IsNullOrWhiteSpace(uploadId))
        {
            throw new ArgumentException("Upload ID cannot be empty", nameof(uploadId));
        }

        if (string.IsNullOrWhiteSpace(subfolder))
        {
            throw new ArgumentException("Subfolder cannot be empty", nameof(subfolder));
        }

        if (string.IsNullOrWhiteSpace(targetFilename))
        {
            throw new ArgumentException("Target filename cannot be empty", nameof(targetFilename));
        }

        // Validate temp file exists
        var tempFilePath = Path.Combine(_storageConfig.TempPath, uploadId);
        if (!File.Exists(tempFilePath))
        {
            _logger.LogWarning("Temp file not found for upload ID: {UploadId}", uploadId);
            throw new FileNotFoundException($"Temporary file not found for upload ID: {uploadId}");
        }

        var metadataPath = $"{tempFilePath}.metadata";
        string originalFilename = uploadId;

        if (File.Exists(metadataPath))
        {
            try
            {
                var metadataContent = await File.ReadAllTextAsync(metadataPath);
                var lines = metadataContent.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("filename ", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(' ', 2);
                        if (parts.Length == 2)
                        {
                            // Decode base64 if needed
                            try
                            {
                                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1].Trim()));
                                originalFilename = decoded;
                            }
                            catch
                            {
                                originalFilename = parts[1].Trim();
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read metadata for upload ID: {UploadId}", uploadId);
            }
        }

        var finalFilename = BuildFinalFileName(targetFilename, originalFilename);

        var finalSubfolderPath = Path.Combine(_storageConfig.FinalPath, subfolder);
        Directory.CreateDirectory(finalSubfolderPath);

        var finalFilePath = Path.Combine(finalSubfolderPath, finalFilename);

        try
        {
            File.Move(tempFilePath, finalFilePath, overwrite: false);
            _logger.LogInformation(
                "Moved file from temp to final. Upload ID: {UploadId}, Final path: {FinalPath}",
                uploadId,
                finalFilePath
            );

            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
            }

            return Path.Combine(subfolder, finalFilename).Replace("\\", "/");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to move file for upload ID: {UploadId}", uploadId);
            throw new InvalidOperationException($"Failed to move file for upload ID: {uploadId}", ex);
        }
    }

    public Task<bool> FileExistsAsync(string uploadId)
    {
        if (string.IsNullOrWhiteSpace(uploadId))
        {
            return Task.FromResult(false);
        }

        var tempFilePath = Path.Combine(_storageConfig.TempPath, uploadId);
        return Task.FromResult(File.Exists(tempFilePath));
    }

    public string BuildFinalFileName(string targetFilename, string originalName)
    {
        var extension = Path.GetExtension(originalName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = "";
        }

        return $"{targetFilename}"; //{targetFilename}{extension}
    }

    public Task<int> GetAudioDurationAsync(string uploadId)
    {
        if (string.IsNullOrWhiteSpace(uploadId))
        {
            return Task.FromResult(0);
        }

        var tempFilePath = Path.Combine(_storageConfig.TempPath, uploadId);
        if (!File.Exists(tempFilePath))
        {
            _logger.LogWarning("Temp file not found for upload ID when reading duration: {UploadId}", uploadId);
            return Task.FromResult(0);
        }

        try
        {
            var track = new Track(tempFilePath);
            var durationInSeconds = (int)Math.Round((double)track.Duration);
            
            _logger.LogInformation(
                "Extracted audio duration: {Duration} seconds for upload ID: {UploadId}",
                durationInSeconds,
                uploadId
            );
            
            return Task.FromResult(durationInSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read audio duration for upload ID: {UploadId}", uploadId);
            return Task.FromResult(0);
        }
    }

    public Task<bool> DeleteFileAsync(string relativeFilePath)
    {
        if (string.IsNullOrWhiteSpace(relativeFilePath))
        {
            return Task.FromResult(false);
        }

        try
        {
            var fullPath = Path.Combine(_storageConfig.FinalPath, relativeFilePath);
            
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("File not found for deletion: {FilePath}", fullPath);
                return Task.FromResult(false);
            }

            File.Delete(fullPath);
            _logger.LogInformation("Deleted file: {FilePath}", fullPath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {RelativePath}", relativeFilePath);
            return Task.FromResult(false);
        }
    }
}
