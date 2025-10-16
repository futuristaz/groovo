namespace Groovo.Models;

public class Playlist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Picture { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int[] Owners { get; set; } = [];
    public List<Song> Songs { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int TotalTime { get; set; }
}
