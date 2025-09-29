using System.Numerics;

namespace Groovo.Models;

public class Song
{
  public string Picture { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public Author[] Authors { get; set; } = [];
  public bool IsActive { get; set; } = true;
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public int Length { get; set; }
}