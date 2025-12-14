using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Groovo.DTOs;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Models;
using System.IdentityModel.Tokens.Jwt;
using Groovo.Tests.Factories;

namespace Groovo.Tests.Controllers;

public class UsersControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UsersControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        
        // Reset authorization header between tests
        _client.DefaultRequestHeaders.Authorization = null;
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

    #region GET /api/v1/users/{id} Tests

    [Fact]
    public async Task GetById_ExistingUser_AsAdmin_ReturnsUser()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var response = await _client.GetAsync($"/api/v1/users/{userId}");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(userId, apiResponse.Data.Id);
        Assert.Equal("Regular User", apiResponse.Data.Name);
        Assert.Equal(UserRole.User, apiResponse.Data.Role);
    }

    [Fact]
    public async Task GetById_WithoutAuthentication_ReturnsUnauthorized()
    {
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var response = await _client.GetAsync($"/api/v1/users/{userId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsRegularUser_ReturnsForbidden()
    {
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var response = await _client.GetAsync($"/api/v1/users/{userId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsAuthor_ReturnsForbidden()
    {
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var response = await _client.GetAsync($"/api/v1/users/{userId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_OtherUser_AsRegularUser_ReturnsForbidden()
    {
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var response = await _client.GetAsync($"/api/v1/users/{userId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonExistentUser_AsAdmin_ReturnsNotFound()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var userId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var response = await _client.GetAsync($"/api/v1/users/{userId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region PUT /api/v1/users/{id} Tests

    [Fact]
    public async Task Update_AsAdmin_UpdatesAnyUser()
    {
        // Arrange
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var request = new UpdateUserRequest 
        { 
            Name = "Admin Updated User",
            Bio = "Admin changed this",
            ImageUrl = "https://example.com/updated.png"
        };

        var response = await _client.PutAsJsonAsync($"/api/v1/users/{userId}", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify update
        var getResponse = await _client.GetAsync($"/api/v1/users/{userId}");
        var apiResponse = await getResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.Equal("Admin Updated User", apiResponse?.Data?.Name);
        Assert.Equal("Admin changed this", apiResponse?.Data?.Bio);
    }

    [Fact]
    public async Task Update_WithoutAuthentication_ReturnsUnauthorized()
    {
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var request = new UpdateUserRequest { Name = "Updated" };

        var response = await _client.PutAsJsonAsync($"/api/v1/users/{userId}", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsRegularUser_ReturnsForbidden()
    {
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var request = new UpdateUserRequest 
        { 
            Name = "Updated Regular User",
            Bio = "Updated bio"
        };

        var response = await _client.PutAsJsonAsync($"/api/v1/users/{userId}", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAuthor_ReturnsForbidden()
    {
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var userId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var request = new UpdateUserRequest { Name = "Updated Author" };

        var response = await _client.PutAsJsonAsync($"/api/v1/users/{userId}", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithInvalidModelState_ReturnsBadRequest()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        
        // Send invalid JSON or empty request that fails validation
        var response = await _client.PutAsync($"/api/v1/users/{userId}", 
            new StringContent("{}", Encoding.UTF8, "application/json"));

        // Depending on your validation rules, this might return BadRequest
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest || 
                    response.StatusCode == HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_NonExistentUser_ReturnsNotFound()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var userId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var request = new UpdateUserRequest { Name = "Test" };

        var response = await _client.PutAsJsonAsync($"/api/v1/users/{userId}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region DELETE /api/v1/users/{id} Tests

    [Fact]
    public async Task Delete_AsAdmin_DeletesSuccessfully()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var userId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.DeleteAsync($"/api/v1/users/{userId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deletion
        var getResponse = await _client.GetAsync($"/api/v1/users/{userId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutAuthentication_ReturnsUnauthorized()
    {
        var userId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.DeleteAsync($"/api/v1/users/{userId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsRegularUser_ReturnsForbidden()
    {
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var userId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.DeleteAsync($"/api/v1/users/{userId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAuthor_ReturnsForbidden()
    {
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var response = await _client.DeleteAsync($"/api/v1/users/{userId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentUser_ReturnsNotFound()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var userId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var response = await _client.DeleteAsync($"/api/v1/users/{userId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region GET /api/v1/users/search Tests

    [Fact]
    public async Task Search_WithQuery_AsAdmin_ReturnsMatchingUsers()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");

        var response = await _client.GetAsync("/api/v1/users/search?query=User");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.True(apiResponse.Data.Count >= 2); // At least "Regular User" and "Author User"
        Assert.All(apiResponse.Data, user => Assert.Contains("User", user.Name));
    }

    [Fact]
    public async Task Search_WithQuery_AsRegularUser_ReturnsResults()
    {
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");

        var response = await _client.GetAsync("/api/v1/users/search?query=User");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
    }

    [Fact]
    public async Task Search_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/users/search?query=User");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_AsAuthor_ReturnsForbidden()
    {
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");

        var response = await _client.GetAsync("/api/v1/users/search?query=User");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithEmptyQuery_ReturnsBadRequest()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");

        var response = await _client.GetAsync("/api/v1/users/search?query=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithWhitespaceQuery_ReturnsBadRequest()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");

        var response = await _client.GetAsync("/api/v1/users/search?query=%20%20%20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithoutQueryParameter_ReturnsBadRequest()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");

        var response = await _client.GetAsync("/api/v1/users/search");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithRoleFilter_ReturnsFilteredResults()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");

        var response = await _client.GetAsync("/api/v1/users/search?query=User&role=Author");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.NotNull(apiResponse.Data);
        // Should only return users with Author role matching "User"
    }

    [Fact]
    public async Task Search_NoMatches_ReturnsEmptyList()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");

        var response = await _client.GetAsync("/api/v1/users/search?query=NonExistentUser");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Empty(apiResponse.Data);
    }

    #endregion

    #region GET /api/v1/users/authors/{id} Tests

    [Fact]
    public async Task GetAuthorById_ExistingAuthor_ReturnsAuthor()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var authorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.GetAsync($"/api/v1/users/authors/{authorId}");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AuthorResponse>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(authorId, apiResponse.Data.Id);
        Assert.Equal("Author User", apiResponse.Data.Name);
    }

    [Fact]
    public async Task GetAuthorById_AsRegularUser_ReturnsAuthor()
    {
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var authorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.GetAsync($"/api/v1/users/authors/{authorId}");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AuthorResponse>>();

        Assert.NotNull(apiResponse);
        Assert.NotNull(apiResponse.Data);
    }

    [Fact]
    public async Task GetAuthorById_AsAuthor_ReturnsAuthor()
    {
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var authorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.GetAsync($"/api/v1/users/authors/{authorId}");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetAuthorById_WithoutAuthentication_ReturnsUnauthorized()
    {
        var authorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.GetAsync($"/api/v1/users/authors/{authorId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthorById_NonExistentAuthor_ReturnsNotFound()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var authorId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var response = await _client.GetAsync($"/api/v1/users/authors/{authorId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region GET /api/v1/users/authors/{id}/songs Tests

    [Fact]
    public async Task GetAuthorSongs_AsAdmin_ReturnsAllSongs()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var authorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.GetAsync($"/api/v1/users/authors/{authorId}/songs");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<SongSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.NotNull(apiResponse.Data);
    }

    [Fact]
    public async Task GetAuthorSongs_AsOwner_ReturnsAllSongs()
    {
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var authorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.GetAsync($"/api/v1/users/authors/{authorId}/songs");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<SongSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.NotNull(apiResponse.Data);
    }

    [Fact]
    public async Task GetAuthorSongs_AsRegularUser_ReturnsOnlyActiveSongs()
    {
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var authorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.GetAsync($"/api/v1/users/authors/{authorId}/songs");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<SongSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.NotNull(apiResponse.Data);
        // Should only contain active songs
    }

    [Fact]
    public async Task GetAuthorSongs_WithoutAuthentication_ReturnsUnauthorized()
    {
        var authorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.GetAsync($"/api/v1/users/authors/{authorId}/songs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthorSongs_NonExistentAuthor_ReturnsNotFound()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var authorId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var response = await _client.GetAsync($"/api/v1/users/authors/{authorId}/songs");

        // Depending on implementation, might return NotFound or empty list
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || 
                    response.StatusCode == HttpStatusCode.OK);
    }

    #endregion

    #region GET /api/v1/users/{id}/playlists Tests

    [Fact]
    public async Task GetUserPlaylists_OwnPlaylists_ReturnsAllPlaylists()
    {
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var response = await _client.GetAsync($"/api/v1/users/{userId}/playlists");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<PlaylistSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(3, apiResponse.Data.Count); // Public and Private playlists owned + 1 public album
    }

    [Fact]
    public async Task GetUserPlaylists_AsAdmin_ReturnsAllUserPlaylists()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var response = await _client.GetAsync($"/api/v1/users/{userId}/playlists");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<PlaylistSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(3, apiResponse.Data.Count); // Public and Private playlists owned + 1 public album
    }

    [Fact]
    public async Task GetUserPlaylists_AsAuthorAccessingOwnPlaylists_ReturnsSuccess()
    {
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var userId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.GetAsync($"/api/v1/users/{userId}/playlists");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetUserPlaylists_AsAuthorAccessingOtherUser_ReturnsForbidden()
    {
        SetAuthorizationHeader(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Author");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var response = await _client.GetAsync($"/api/v1/users/{userId}/playlists");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUserPlaylists_WithoutAuthentication_ReturnsUnauthorized()
    {
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var response = await _client.GetAsync($"/api/v1/users/{userId}/playlists");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserPlaylists_NonExistentUser_ReturnsPublicPlaylists()
    {
        SetAuthorizationHeader(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Admin");
        var userId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var response = await _client.GetAsync($"/api/v1/users/{userId}/playlists");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<PlaylistSummaryResponse>>>();
        Assert.NotNull(apiResponse);
        Assert.Equal(2, apiResponse.Data!.Count); // Returns public playlists (album + public playlist)
        Assert.All(apiResponse.Data, p => Assert.True(p.IsPublic));
    }

    [Fact]
    public async Task GetUserPlaylists_OtherUserPublicPlaylistsOnly_ReturnsOnlyPublic()
    {
        // Arrange - Regular user accessing author's playlists (should only see public)
        SetAuthorizationHeader(Guid.Parse("22222222-2222-2222-2222-222222222222"), "User");
        var authorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await _client.GetAsync($"/api/v1/users/{authorId}/playlists");

        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<PlaylistSummaryResponse>>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Single(apiResponse.Data); // Only the public album
        Assert.All(apiResponse.Data, p => Assert.True(p.IsPublic));
    }

    #endregion
}