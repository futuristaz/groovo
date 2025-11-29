namespace Groovo.Services;

public interface ISongFileService
{
    /// <summary>
    /// Moves an uploaded file from temp to final storage
    /// </summary>
    /// <param name="uploadId">The tus upload ID</param>
    /// <param name="subfolder">Subfolder in final storage (e.g., "audio" or "images")</param>
    /// <param name="targetFilename">Target filename (without extension) - typically the song UUID</param>
    /// <returns>The final file path relative to final storage</returns>
    Task<string> MoveUploadedFileAsync(string uploadId, string subfolder, string targetFilename);

    /// <summary>
    /// Checks if a file exists in temporary storage
    /// </summary>
    /// <param name="uploadId">The tus upload ID</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> FileExistsAsync(string uploadId);

    /// <summary>
    /// Builds a final filename based on target filename and original extension
    /// </summary>
    /// <param name="targetFilename">Target filename (without extension)</param>
    /// <param name="originalName">Original filename from metadata to extract extension</param>
    /// <returns>A unique filename</returns>
    string BuildFinalFileName(string targetFilename, string originalName);

    /// <summary>
    /// Gets the duration of an audio file in seconds from temporary storage
    /// </summary>
    /// <param name="uploadId">The tus upload ID</param>
    /// <returns>Duration in seconds, or 0 if unable to read</returns>
    Task<int> GetAudioDurationAsync(string uploadId);

    /// <summary>
    /// Deletes a file from final storage
    /// </summary>
    /// <param name="relativeFilePath">The relative file path (e.g., "audio/file.mp3" or "images/file.jpg")</param>
    /// <returns>True if file was deleted, false if file didn't exist</returns>
    Task<bool> DeleteFileAsync(string relativeFilePath);
}