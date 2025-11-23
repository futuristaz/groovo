namespace Groovo.Services;

public interface ISongFileService
{
    /// <summary>
    /// Moves an uploaded file from temp to final storage
    /// </summary>
    /// <param name="uploadId">The tus upload ID</param>
    /// <param name="subfolder">Subfolder in final storage (e.g., "audio" or "images")</param>
    /// <returns>The final file path relative to final storage</returns>
    Task<string> MoveUploadedFileAsync(string uploadId, string subfolder);

    /// <summary>
    /// Checks if a file exists in temporary storage
    /// </summary>
    /// <param name="uploadId">The tus upload ID</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> FileExistsAsync(string uploadId);

    /// <summary>
    /// Builds a final filename based on the upload ID and original name
    /// </summary>
    /// <param name="uploadId">The tus upload ID</param>
    /// <param name="originalName">Original filename from metadata</param>
    /// <returns>A unique filename</returns>
    string BuildFinalFileName(string uploadId, string originalName);

    /// <summary>
    /// Gets the duration of an audio file in seconds from temporary storage
    /// </summary>
    /// <param name="uploadId">The tus upload ID</param>
    /// <returns>Duration in seconds, or 0 if unable to read</returns>
    Task<int> GetAudioDurationAsync(string uploadId);
}