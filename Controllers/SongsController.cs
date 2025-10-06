using Microsoft.AspNetCore.Mvc;
using Groovo.Models;

namespace Groovo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SongsController : ControllerBase
    {
        // Laikinas dainų sarašas atmintyje
        private static readonly List<Song> Songs = new();

        // GET: api/songs
        // Gražina visas dainas atmintyje įkeltas tik at runtime
        [HttpGet]
        public ActionResult<IEnumerable<Song>> GetAll()
        {
            return Ok(Songs);
        }

        // GET: api/songs/{id}
        // gražina specifinę dainą arba 404 jeigu nerado
        [HttpGet("{id:guid}")]
        public ActionResult<Song> GetById(Guid id)
        {
            var song = Songs.FirstOrDefault(s => s.Id == id);
            if (song == null)
                return NotFound($"Song with ID {id} not found.");

            return Ok(song);
        }

        // POST: api/songs
        // Įkelia daina į atmintį, duoda jai {id} ir gražina žinute, kad įkelė (201 su link)
        [HttpPost]
        public ActionResult<Song> Create([FromBody] Song newSong)
        {
            newSong.Id = Guid.NewGuid();
            newSong.CreatedAt = DateTime.UtcNow;
            newSong.UpdatedAt = DateTime.UtcNow;

            Songs.Add(newSong);

            return CreatedAtAction(nameof(GetById), new { id = newSong.Id }, newSong);
        }

        // PUT: api/songs/{id}
        // Atnaujina jau egzistuojančios dainos duomenis, 204 ir no link jei all good, 404 otherwise
        [HttpPut("{id:guid}")]
        public ActionResult Update(Guid id, [FromBody] Song updatedSong)
        {
            var existing = Songs.FirstOrDefault(s => s.Id == id);
            if (existing == null)
                return NotFound($"Song with ID {id} not found.");

            existing.Name = updatedSong.Name;
            existing.Description = updatedSong.Description;
            existing.Picture = updatedSong.Picture;
            existing.Authors = updatedSong.Authors;
            existing.Album = updatedSong.Album;
            existing.Genre = updatedSong.Genre;
            existing.Tags = updatedSong.Tags;
            existing.AudioUrl = updatedSong.AudioUrl;
            existing.IsActive = updatedSong.IsActive;
            existing.Length = updatedSong.Length;
            existing.Plays = updatedSong.Plays;
            existing.Likes = updatedSong.Likes;
            existing.UpdatedAt = DateTime.UtcNow;

            return NoContent();
        }

        // DELETE: api/songs/{id}
        // 204 ir no link jei all good, 404 otherwise
        [HttpDelete("{id:guid}")]
        public ActionResult Delete(Guid id)
        {
            var song = Songs.FirstOrDefault(s => s.Id == id);
            if (song == null)
                return NotFound($"Song with ID {id} not found.");

            Songs.Remove(song);
            return NoContent();
        }
    }
}
