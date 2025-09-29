using System.Numerics;

namespace Groovo.Models;

public class Playlist
{
  public string Picture { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public int[] Owners { get; set; } = [];
  public bool IsActive { get; set; } = true;
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public int TotalTime { get; set; }
}