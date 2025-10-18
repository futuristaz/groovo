namespace Groovo.Models;

[Index(nameof(SongId), nameof(AuthorId), IsUnique = true)]
public class SongAuthor
{
    public Guid SongId { get; set; }
    public Guid UserId { get; set; }
    
    // Navigation properties
    public Song Song { get; set; }
    public User User { get; set; }
}