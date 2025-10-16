namespace Groovo.Models;

public class Song
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Picture { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Author[] Authors { get; set; } = [];
    public string Album { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string AudioUrl { get; set; } = string.Empty;
    public List<Playlist> PlaylistIds { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int Length { get; set; }
    public int Plays { get; set; } = 0;
    public int Likes { get; set; } = 0;
}
