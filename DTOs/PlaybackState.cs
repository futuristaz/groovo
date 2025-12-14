namespace Groovo.DTOs;

public class PlaybackState
{
    public bool IsPlaying { get; set; } = false;
    public Guid? CurrentSongId { get; set; }
    public Guid? NextSongId { get; set; }
    public int CurrentPosition { get; set; } = 0;
    public int CurrentLength { get; set; } = 0;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public int CurrentlyListening { get; set; } = 0;
    public bool IsShuffleEnabled { get; set; } = false;
    public int? ShuffleSeed { get; set; }
}