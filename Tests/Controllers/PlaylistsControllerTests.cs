using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Groovo;
using Groovo.Data.Contexts;
using Groovo.DTOs;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Models;
using System.IdentityModel.Tokens.Jwt;
using Groovo.Tests.Factories;

namespace Groovo.Tests.Controllers;

public class PlaylistsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PlaylistsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    #region Helper Methods

    private string GenerateJwtToken(Guid userId, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: CustomWebApplicationFactory.JwtIssuer,
            audience: CustomWebApplicationFactory.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private void SetAuthorizationHeader(Guid userId, string role)
    {
        var token = GenerateJwtToken(userId, role);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    #endregion

    #region GET /api/v1/playlists Tests

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/playlists");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsRegularUser_ReturnsOnlyPublicPlaylists()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");

        // Act
        var response = await _client.GetAsync("/api/v1/playlists");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<PlaylistSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(2, apiResponse.Data.Count); // Public playlist and album
        Assert.All(apiResponse.Data, p => Assert.True(p.IsPublic));
    }

    [Fact]
    public async Task GetAll_AsAdmin_ReturnsAllPlaylists()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");

        // Act
        var response = await _client.GetAsync("/api/v1/playlists");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<PlaylistSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(3, apiResponse.Data.Count); // All playlists including private
    }

    #endregion

    #region GET /api/v1/playlists/{id} Tests

    [Fact]
    public async Task GetById_PublicPlaylist_ReturnsPlaylist()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var playlistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var response = await _client.GetAsync($"/api/v1/playlists/{playlistId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PlaylistResponse>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(playlistId, apiResponse.Data.Id);
        Assert.Equal("Public Playlist", apiResponse.Data.Name);
    }

    [Fact]
    public async Task GetById_PrivatePlaylistAsOwner_ReturnsPlaylist()
    {
        // Arrange - Regular user is owner of private playlist
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var response = await _client.GetAsync($"/api/v1/playlists/{playlistId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PlaylistResponse>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(playlistId, apiResponse.Data.Id);
        Assert.False(apiResponse.Data.IsPublic);
    }

    [Fact]
    public async Task GetById_PrivatePlaylistAsNonOwner_ReturnsNotFound()
    {
        // Arrange - Author user trying to access regular user's private playlist
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var response = await _client.GetAsync($"/api/v1/playlists/{playlistId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonExistentPlaylist_ReturnsNotFound()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var playlistId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        // Act
        var response = await _client.GetAsync($"/api/v1/playlists/{playlistId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region POST /api/v1/playlists Tests

    [Fact]
    public async Task Create_AsRegularUser_CreatesPlaylist()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var request = new CreatePlaylistRequest
        {
            Name = "New Playlist",
            Description = "Test description",
            IsPublic = true,
            IsAlbum = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/playlists", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PlaylistResponse>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal("New Playlist", apiResponse.Data.Name);
        Assert.False(apiResponse.Data.IsAlbum);
    }

    [Fact]
    public async Task Create_AsAuthor_CreatingPlaylist_ReturnsForbidden()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var request = new CreatePlaylistRequest
        {
            Name = "Author Playlist",
            IsAlbum = false // Authors can't create regular playlists
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/playlists", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAuthor_CreatingAlbum_Succeeds()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var request = new CreatePlaylistRequest
        {
            Name = "New Album",
            Description = "Author's album",
            IsPublic = true,
            IsAlbum = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/playlists", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PlaylistResponse>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.True(apiResponse.Data.IsAlbum);
    }

    [Fact]
    public async Task Create_AsUser_CreatingAlbum_ReturnsForbidden()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var request = new CreatePlaylistRequest
        {
            Name = "User Album",
            IsAlbum = true // Users can't create albums
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/playlists", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_CreatingAnything_Succeeds()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var request = new CreatePlaylistRequest
        {
            Name = "Admin Album",
            IsAlbum = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/playlists", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidModel_ReturnsBadRequest()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var request = new CreatePlaylistRequest
        {
            Name = "", // Invalid - required
            IsAlbum = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/playlists", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region PUT /api/v1/playlists/{id} Tests

    [Fact]
    public async Task Update_AsOwner_UpdatesPlaylist()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var request = new UpdatePlaylistRequest
        {
            Name = "Updated Playlist Name",
            Description = "Updated description"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/playlists/{playlistId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify update
        var getResponse = await _client.GetAsync($"/api/v1/playlists/{playlistId}");
        var apiResponse = await getResponse.Content.ReadFromJsonAsync<ApiResponse<PlaylistResponse>>();
        Assert.Equal("Updated Playlist Name", apiResponse?.Data?.Name);
    }

    [Fact]
    public async Task Update_AsNonOwner_ReturnsNotFound()
    {
        // Arrange - Author trying to update regular user's playlist
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var request = new UpdatePlaylistRequest
        {
            Name = "Unauthorized Update"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/playlists/{playlistId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_UpdatesAnyPlaylist()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var request = new UpdatePlaylistRequest
        {
            Name = "Admin Updated"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/playlists/{playlistId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    #endregion

    #region DELETE /api/v1/playlists/{id} Tests

    [Fact]
    public async Task Delete_EmptyPlaylistAsOwner_DeletesSuccessfully()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var response = await _client.DeleteAsync($"/api/v1/playlists/{playlistId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deletion
        var getResponse = await _client.GetAsync($"/api/v1/playlists/{playlistId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_AsNonOwner_ReturnsNotFound()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var response = await _client.DeleteAsync($"/api/v1/playlists/{playlistId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region POST /api/v1/playlists/{playlistId}/songs/{songId} Tests

    [Fact]
    public async Task AddSongToPlaylist_AsOwner_AddsSuccessfully()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var songId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        // Act
        var response = await _client.PostAsync(
            $"/api/v1/playlists/{playlistId}/songs/{songId}",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AddSongToPlaylist_DuplicateSong_ReturnsConflict()
    {
        // Arrange - Song already in public playlist
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var playlistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var songId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        // Act
        var response = await _client.PostAsync(
            $"/api/v1/playlists/{playlistId}/songs/{songId}",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddSongToPlaylist_NonExistentSong_ReturnsNotFound()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var songId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        // Act
        var response = await _client.PostAsync(
            $"/api/v1/playlists/{playlistId}/songs/{songId}",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region DELETE /api/v1/playlists/{playlistId}/songs/{songId} Tests

    [Fact]
    public async Task RemoveSongFromPlaylist_AsOwner_RemovesSuccessfully()
    {
        // Arrange - First add a song to the private playlist
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var songId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        // Add the song first
        var addResponse = await _client.PostAsync($"/api/v1/playlists/{playlistId}/songs/{songId}", null);
        addResponse.EnsureSuccessStatusCode();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/playlists/{playlistId}/songs/{songId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RemoveSongFromPlaylist_NonExistentSong_ReturnsNotFound()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var songId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        // Act
        var response = await _client.DeleteAsync($"/api/v1/playlists/{playlistId}/songs/{songId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region GET /api/v1/playlists/search Tests

    [Fact]
    public async Task Search_WithQuery_ReturnsMatchingPlaylists()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");

        // Act
        var response = await _client.GetAsync("/api/v1/playlists/search?query=Public");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<PlaylistSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Single(apiResponse.Data);
        Assert.Contains("Public", apiResponse.Data[0].Name);
    }

    [Fact]
    public async Task Search_WithoutQuery_ReturnsBadRequest()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");

        // Act
        var response = await _client.GetAsync("/api/v1/playlists/search?query=");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_NoMatches_ReturnsEmptyList()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");

        // Act
        var response = await _client.GetAsync("/api/v1/playlists/search?query=NonExistent");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<PlaylistSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Empty(apiResponse.Data);
    }

    #endregion

    #region GET /api/v1/playlists/{id}/songs Tests

    [Fact]
    public async Task GetSongsInPlaylist_PublicPlaylist_ReturnsSongs()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var playlistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var response = await _client.GetAsync($"/api/v1/playlists/{playlistId}/songs");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<SongSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Single(apiResponse.Data); // One song was added during seeding
    }

    [Fact]
    public async Task GetSongsInPlaylist_PrivatePlaylistAsOwner_ReturnsSongs()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var response = await _client.GetAsync($"/api/v1/playlists/{playlistId}/songs");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<SongSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Empty(apiResponse.Data); // No songs in private playlist
    }

    [Fact]
    public async Task GetSongsInPlaylist_PrivatePlaylistAsNonOwner_ReturnsNotFound()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var playlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var response = await _client.GetAsync($"/api/v1/playlists/{playlistId}/songs");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion
}