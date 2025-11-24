using Microsoft.EntityFrameworkCore;
using Groovo.Data.Contexts;
using Groovo.DTOs.Responses;
using Groovo.Models;

namespace Groovo.Services
{
    public class AuthorService : IAuthorService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthorService> _logger;

        public AuthorService(ApplicationDbContext context, ILogger<AuthorService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SongResponse?> GetAuthorSongByIdAsync(Guid songId, Guid userId, bool isAdmin)
        {
            try
            {
                var song = await _context.Songs
                    .Include(s => s.SongAuthors)
                    .ThenInclude(sa => sa.User)
                    .Where(s => s.Id == songId && (isAdmin ||
                        s.SongAuthors.Any(sa => sa.UserId == userId)))
                    .FirstOrDefaultAsync();

                if (song == null)
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
