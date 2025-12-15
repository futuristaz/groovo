using Groovo.Repositories;

namespace Groovo.Services.Hub;

public class ShuffleService : IShuffleService
{
    private readonly IPlaylistRepository _playlistRepository;

    public ShuffleService(IPlaylistRepository playlistRepository)
    {
        _playlistRepository = playlistRepository;
    }

    public async Task<Guid?> GetNextShuffledSongAsync(Guid playlistId, Guid currentSongId, int shuffleSeed)
    {
        var songIds = await _playlistRepository.GetAllActiveSongIdsInOrderAsync(playlistId);
        
        if (songIds.Count == 0)
        {
            return null;
        }

        var shuffledIds = ShuffleList(songIds, shuffleSeed);
        
        var currentIndex = shuffledIds.IndexOf(currentSongId);
        
        // If current song not found (was removed), return first song
        if (currentIndex == -1)
        {
            return shuffledIds.FirstOrDefault();
        }
        
        // If at the end, return null (or could loop back to start)
        if (currentIndex >= shuffledIds.Count - 1)
        {
            return null;
        }
        
        return shuffledIds[currentIndex + 1];
    }

    public async Task<Guid?> GetPreviousShuffledSongAsync(Guid playlistId, Guid currentSongId, int shuffleSeed)
    {
        var songIds = await _playlistRepository.GetAllActiveSongIdsInOrderAsync(playlistId);
        
        if (songIds.Count == 0)
        {
            return null;
        }

        var shuffledIds = ShuffleList(songIds, shuffleSeed);
        
        var currentIndex = shuffledIds.IndexOf(currentSongId);
        
        // If current song not found or at beginning, return null
        if (currentIndex <= 0)
        {
            return null;
        }
        
        return shuffledIds[currentIndex - 1];
    }

    public int GenerateShuffleSeed()
    {
        return Random.Shared.Next();
    }

    /// <summary>
    /// Shuffles a list using Fisher-Yates algorithm with a seed for deterministic results
    /// </summary>
    private List<Guid> ShuffleList(List<Guid> list, int seed)
    {
        var shuffled = new List<Guid>(list);
        var random = new Random(seed);
        
        // Fisher-Yates shuffle
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        
        return shuffled;
    }
}
