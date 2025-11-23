namespace Groovo.DTOs;

internal class PlaybackState
{
    public bool IsPlaying { get; set; }
    public Guid? CurrentSongId { get; set; }
    public int CurrentPosition { get; set; }
    public DateTime LastUpdated { get; set; }
}