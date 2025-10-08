using Microsoft.AspNetCore.Mvc;
using Groovo.Models;
using Groovo.Data;

namespace Groovo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlaylistController : ControllerBase
    {
        // Singleton objektas, kuris laiko bendrą dainų ir playlistų sąrašą
        private readonly InMemoryDatabase database = InMemoryDatabase.Instance;

        // GET: api/playlists
        // Gražina visus playistus atmintyje įkeltas tik at runtime
        [HttpGet]
        public ActionResult<IEnumerable<Playlist>> GetAll()
        {
            return Ok(database.Playlists);
        }

        // GET: api/playlists/{id}
        // Gražina specifinį playlist arba 404 jeigu nerado
        [HttpGet("{id:guid}")]
        public ActionResult<Playlist> GetById(Guid id)
        {
            var playlist = database.Playlists.FirstOrDefault(p => p.Id == id);
            if (playlist == null)
                return NotFound($"Playlist with ID {id} not found.");

            return Ok(playlist);
        }

        // POST: api/playlists
        // Įkelia playlist į atmintį, duoda{id} ir gražina žinute, kad įkelė (201 su link)
        [HttpPost]
        public ActionResult<Playlist> Create([FromBody] Playlist newPlaylist)
        {
            newPlaylist.Id = Guid.NewGuid();
            newPlaylist.CreatedAt = DateTime.UtcNow;
            newPlaylist.UpdatedAt = DateTime.UtcNow;
            database.Playlists.Add(newPlaylist);

            return CreatedAtAction(nameof(GetById), new { id = newPlaylist.Id }, newPlaylist);
        }

        // PUT: api/playlists/{id}
        // Atnaujina jau egzistuojančio playlist duomenis, 204 ir no link jei all good, 404 otherwise
        [HttpPut("{id:guid}")]
        public ActionResult Update(Guid id, [FromBody] Playlist updatedPlaylist)
        {
            var existing = database.Playlists.FirstOrDefault(p => p.Id == id);
            if (existing == null)
                return NotFound($"Playlist with ID {id} not found.");

            existing.Name = updatedPlaylist.Name;
            existing.Description = updatedPlaylist.Description;
            existing.Picture = updatedPlaylist.Picture;
            existing.Owners = updatedPlaylist.Owners;
            existing.Songs = updatedPlaylist.Songs;
            existing.IsActive = updatedPlaylist.IsActive;
            existing.IsPublic = updatedPlaylist.IsPublic;
            existing.TotalTime = updatedPlaylist.TotalTime;
            existing.UpdatedAt = DateTime.UtcNow;

            return NoContent();
        }

        // DELETE: api/playlists/{id}
        // 204 ir no link jei all good, 404 otherwise
        [HttpDelete("{id:guid}")]
        public ActionResult Delete(Guid id)
        {
            var playlist = database.Playlists.FirstOrDefault(p => p.Id == id);
            if (playlist == null)
                return NotFound($"Playlist with ID {id} not found.");

            database.Playlists.Remove(playlist);
            return NoContent();
        }

        //=============================================
        // Toliau yra veiksmai tarp playlists ir songs
        //=============================================

        // GET: api/playlists/{id}/songs
        [HttpGet("{id:guid}/songs")]
        public ActionResult<IEnumerable<Song>> GetSongsInPlaylist(Guid id)
        {
            var playlist = database.Playlists.FirstOrDefault(p => p.Id == id);
            if (playlist == null)
                return NotFound($"Playlist with ID {id} not found.");

            return Ok(playlist.Songs);
        }

        // POST: api/playlists/{playlistId}/songs/{songId}
        [HttpPost("{playlistId:guid}/songs/{songId:guid}")]
        public ActionResult AddSongToPlaylist(Guid playlistId, Guid songId)
        {
            var playlist = database.Playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist == null)
                return NotFound($"Playlist with ID {playlistId} not found.");

            var song = database.Songs.FirstOrDefault(s => s.Id == songId);
            if (song == null)
                return NotFound($"Song with ID {songId} not found.");

            if (playlist.Songs.Any(s => s.Id == songId))
                return Conflict("Song already in playlist.");

            playlist.Songs.Add(song);
            playlist.UpdatedAt = DateTime.UtcNow;
            playlist.TotalTime = playlist.Songs.Sum(s => s.Length);

            return Ok($"Added song '{song.Name}' to playlist '{playlist.Name}'.");
        }

        // DELETE: api/playlists/{playlistId}/songs/{songId}
        [HttpDelete("{playlistId:guid}/songs/{songId:guid}")]
        public ActionResult RemoveSongFromPlaylist(Guid playlistId, Guid songId)
        {
            var playlist = database.Playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist == null)
                return NotFound($"Playlist with ID {playlistId} not found.");

            var song = playlist.Songs.FirstOrDefault(s => s.Id == songId);
            if (song == null)
                return NotFound($"Song with ID {songId} not found in this playlist.");

            playlist.Songs.Remove(song);
            playlist.TotalTime = playlist.Songs.Sum(s => s.Length);
            playlist.UpdatedAt = DateTime.UtcNow;

            return Ok($"Removed song '{song.Name}' from playlist '{playlist.Name}'.");
        }
    }
}
