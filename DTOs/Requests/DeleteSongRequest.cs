using System;
using System.ComponentModel.DataAnnotations;

namespace Groovo.DTOs.Requests
{
    public class RemoveSongRequest
    {
        [Required]
        public Guid PlaylistId { get; set; }

        [Required]
        public Guid SongId { get; set; }
    }
}
