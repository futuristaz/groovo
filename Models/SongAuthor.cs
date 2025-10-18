using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Groovo.Models;

[Index(nameof(SongId), nameof(UserId), IsUnique = true)]
[PrimaryKey(nameof(SongId), nameof(UserId))]
public class SongAuthor
{
    public Guid SongId { get; set; }
    public Guid UserId { get; set; }
    
    // Navigation properties
    public Song Song { get; set; }
    public User User { get; set; }
}