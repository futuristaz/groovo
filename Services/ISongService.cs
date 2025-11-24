using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;

namespace Groovo.Services
{
    public interface ISongService
    {
        Task<SongResponse?> GetSongByIdAsync(Guid id);
        Task<(SongResponse? Song, string? ErrorMessage)> CreateSongAsync(CreateSongRequest request, Guid? authorId = null, bool isAdmin = false);
        Task<(bool Success, string? ErrorMessage)> UpdateSongAsync(Guid id, UpdateSongRequest request, Guid? userId = null, bool isAdmin = false);
        Task<bool> DeleteSongAsync(Guid id, Guid? userId = null, bool isAdmin = false);
        Task<List<SongSummaryResponse>> SearchSongsAsync(string query);
    }
}
