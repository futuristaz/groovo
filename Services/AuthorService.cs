using Groovo.DTOs.Responses;
using Groovo.Models;
using Groovo.Repositories;

namespace Groovo.Services
{
    public class AuthorService : IAuthorService
    {
        private readonly ILogger<AuthorService> _logger;
        private readonly ISongRepository _songRepository;

        public AuthorService(ILogger<AuthorService> logger, ISongRepository songRepository)
        {
            _logger = logger;
            _songRepository = songRepository;
        }

        public async Task<SongResponse?> GetAuthorSongByIdAsync(Guid songId, Guid userId, bool isAdmin)
        {
            try
            {
                var song = await _songRepository.GetByIdAsync(songId, includeAuthors: true);

                if (song == null)
                    return null;

                // Check permissions
                if (!isAdmin && !song.SongAuthors.Any(sa => sa.UserId == userId))
                    return null;

                var songResponse = new SongResponse(
                    song,
                    song.SongAuthors.Where(sa => sa.User.Role == UserRole.Author).Select(sa => new AuthorResponse(
                        sa.User.Id,
                        sa.User.Name,
                        sa.User.Bio,
                        sa.User.ImageUrl
                    )).ToList()
                );

                return songResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving song {SongId} for user {UserId}", songId, userId);
                throw;
            }
        }
    }
}
