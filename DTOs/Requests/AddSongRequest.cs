using System;
using System.ComponentModel.DataAnnotations;

namespace Groovo.DTOs.Requests
{
    // Request to add a song to a playlist
    public class AddSongRequest
    {
        [Required]
        public Guid PlaylistId { get; set; }

        [Required]
        public Guid SongId { get; set; }
    }

}
