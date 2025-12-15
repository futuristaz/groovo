namespace Groovo.Services.Hub;

public interface IShuffleService
{
    Task<Guid?> GetNextShuffledSongAsync(Guid playlistId, Guid currentSongId, int shuffleSeed);
    Task<Guid?> GetPreviousShuffledSongAsync(Guid playlistId, Guid currentSongId, int shuffleSeed);
    int GenerateShuffleSeed();
}
