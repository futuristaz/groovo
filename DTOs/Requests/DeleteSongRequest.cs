using System;
using System.ComponentModel.DataAnnotations;

namespace Groovo.DTOs.Requests
{

    // Request to remove a song from a playlist
    public class RemoveSongRequest
    {
        [Required]
        public Guid PlaylistId { get; set; }

        [Required]
        public Guid SongId { get; set; }
    }
}
