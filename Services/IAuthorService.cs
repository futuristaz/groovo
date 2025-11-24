using Groovo.DTOs.Responses;

namespace Groovo.Services
{
    public interface IAuthorService
    {
        Task<SongResponse?> GetAuthorSongByIdAsync(Guid songId, Guid userId, bool isAdmin);
    }
}
