using Microsoft.AspNetCore.Mvc;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Services;
using Groovo.Exceptions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Groovo.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class SearchController : ControllerBase
    {
        private readonly IPlaylistService _playlistService;
        private readonly ISongService _songService;
        private readonly IUserService _userService;
        private readonly ILogger<SearchController> _logger;

        public SearchController(
            IPlaylistService playlistService,
            ISongService songService,
            IUserService userService,
            ILogger<SearchController> logger)
        {
            _playlistService = playlistService;
            _songService = songService;
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// GET: /api/v1/search
        /// Gives 
        /// </summary>
        /// <returns>List of users, list of songs, list of playlists</returns>
        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<ActionResult<GenericSearchResponse>> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query parameter is required");
            }

            try
            {
                var songTask     = _songService.SearchSongsAsync(query);
                var userTask     = _userService.SearchUsersAsync(query);
                var playlistTask = _playlistService.SearchPlaylistsAsync(query);

                await Task.WhenAll(songTask, userTask, playlistTask);

                return Ok(new GenericSearchResponse
                {
                    Songs     = songTask.Result,
                    Users     = userTask.Result,
                    Playlists = playlistTask.Result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching songs with query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
