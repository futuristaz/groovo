using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Groovo.DTOs;
using Groovo.DTOs.Responses;
using System.IdentityModel.Tokens.Jwt;
using Groovo.Tests.Factories;

namespace Groovo.Tests.Controllers;

public class AuthorControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    // Known test user IDs from seeder
    private readonly Guid _adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _regularUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _authorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    
    // Known song IDs from seeder
    private readonly Guid _song1Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private readonly Guid _song2Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    public AuthorControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
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

    #region GET /api/v1/author/songs/{id} Tests - Authorization

    [Fact]
    public async Task GetSongById_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var response = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSongById_AsRegularUser_ReturnsForbidden()
    {
        // Arrange
        SetAuthorizationHeader(_regularUserId, "User");

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSongById_InvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSongById_ExpiredToken_ReturnsUnauthorized()
    {
        // Arrange - Create expired token
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _adminId.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiredToken = new JwtSecurityToken(
            issuer: CustomWebApplicationFactory.JwtIssuer,
            audience: CustomWebApplicationFactory.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(expiredToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET /api/v1/author/songs/{id} Tests - Valid Scenarios

    [Fact]
    public async Task GetSongById_AsAdmin_ExistingSong_ReturnsSong()
    {
        // Arrange
        SetAuthorizationHeader(_adminId, "Admin");

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<SongResponse>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(_song1Id, apiResponse.Data.Id);
        Assert.Equal("Test Song 1", apiResponse.Data.Name);
    }

    [Fact]
    public async Task GetSongById_AsAuthorOwner_ExistingSong_ReturnsSong()
    {
        // Arrange - Author accessing their own song
        SetAuthorizationHeader(_authorId, "Author");

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<SongResponse>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(_song1Id, apiResponse.Data.Id);
        Assert.Contains(apiResponse.Data.Authors, a => a.Id == _authorId);
    }

    [Fact]
    public async Task GetSongById_AsAuthorNonOwner_ExistingSong_ReturnsNotFound()
    {
        // Arrange - Different author trying to access song they don't own
        var differentAuthorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        SetAuthorizationHeader(differentAuthorId, "Author");

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSongById_NonExistentSong_AsAdmin_ReturnsNotFound()
    {
        // Arrange
        SetAuthorizationHeader(_adminId, "Admin");
        var nonExistentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(content))
        {
            Assert.Contains("not found", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GetSongById_NonExistentSong_AsAuthor_ReturnsNotFound()
    {
        // Arrange
        SetAuthorizationHeader(_authorId, "Author");
        var nonExistentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSongById_EmptyGuid_ReturnsNotFound()
    {
        // Arrange
        SetAuthorizationHeader(_adminId, "Admin");
        var emptyGuid = Guid.Empty;

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{emptyGuid}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSongById_InvalidGuid_ReturnsNotFound()
    {
        // Arrange
        SetAuthorizationHeader(_adminId, "Admin");
        var invalidGuid = "not-a-guid";

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{invalidGuid}");

        // Assert
        // ASP.NET Core routing returns 404 for invalid GUID in route constraint
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region GET /api/v1/author/songs/{id} Tests - Response Structure

    [Fact]
    public async Task GetSongById_AsAdmin_ReturnsSongWithAllDetails()
    {
        // Arrange
        SetAuthorizationHeader(_adminId, "Admin");

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<SongResponse>>();

        Assert.NotNull(apiResponse);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(_song1Id, apiResponse.Data.Id);
        Assert.NotEmpty(apiResponse.Data.Name);
        Assert.NotNull(apiResponse.Data.Authors);
        Assert.NotEmpty(apiResponse.Data.Authors);
        Assert.Equal("Test Song 1", apiResponse.Data.Name);
    }

    [Fact]
    public async Task GetSongById_AsAuthorOwner_ReturnsSongWithAllDetails()
    {
        // Arrange
        SetAuthorizationHeader(_authorId, "Author");

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{_song2Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<SongResponse>>();

        Assert.NotNull(apiResponse);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(_song2Id, apiResponse.Data.Id);
        Assert.Equal("Test Song 2", apiResponse.Data.Name);
        // Verify the song belongs to this author
        Assert.Contains(apiResponse.Data.Authors, a => a.Id == _authorId);
    }

    [Fact]
    public async Task GetSongById_VerifyResponseStructure()
    {
        // Arrange
        SetAuthorizationHeader(_adminId, "Admin");

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<SongResponse>>();

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        
        // Verify structure
        var song = apiResponse.Data;
        Assert.NotEqual(Guid.Empty, song.Id);
        Assert.NotNull(song.Name);
        Assert.NotEmpty(song.Name);
        Assert.NotNull(song.Authors);
        Assert.NotEmpty(song.Authors);
    }

    [Fact]
    public async Task GetSongById_AdminCanAccessAllSongDetails()
    {
        // Arrange
        SetAuthorizationHeader(_adminId, "Admin");

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<SongResponse>>();

        Assert.NotNull(apiResponse?.Data);
        Assert.Equal(_song1Id, apiResponse.Data.Id);
        // Admin should get full song details
        Assert.NotNull(apiResponse.Data.Authors);
        Assert.NotEmpty(apiResponse.Data.Authors);
    }

    [Fact]
    public async Task GetSongById_AuthorGetsFullDetailsForOwnSong()
    {
        // Arrange
        SetAuthorizationHeader(_authorId, "Author");

        // Act
        var response = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<SongResponse>>();

        Assert.NotNull(apiResponse?.Data);
        Assert.Equal(_song1Id, apiResponse.Data.Id);
        // Verify author is in the authors list
        Assert.Contains(apiResponse.Data.Authors, a => a.Id == _authorId);
    }

    [Fact]
    public async Task GetSongById_AdminCanAccessMultipleSongs()
    {
        // Arrange
        SetAuthorizationHeader(_adminId, "Admin");

        // Act - Access first song
        var response1 = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");
        
        // Check if response is successful before deserializing
        if (!response1.IsSuccessStatusCode)
        {
            Assert.Fail($"First song request failed with status code: {response1.StatusCode}");
        }
        
        var apiResponse1 = await response1.Content.ReadFromJsonAsync<ApiResponse<SongResponse>>();

        // Act - Access second song
        var response2 = await _client.GetAsync($"/api/v1/author/songs/{_song2Id}");
        var apiResponse2 = await response2.Content.ReadFromJsonAsync<ApiResponse<SongResponse>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.NotNull(apiResponse1?.Data);
        Assert.NotNull(apiResponse2?.Data);
        Assert.Equal(_song1Id, apiResponse1.Data.Id);
        Assert.Equal(_song2Id, apiResponse2.Data.Id);
    }

    [Fact]
    public async Task GetSongById_AuthorCanAccessAllOwnSongs()
    {
        // Arrange
        SetAuthorizationHeader(_authorId, "Author");

        // Act - Access first song
        var response1 = await _client.GetAsync($"/api/v1/author/songs/{_song1Id}");
        
        // Act - Access second song
        var response2 = await _client.GetAsync($"/api/v1/author/songs/{_song2Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        
        var apiResponse1 = await response1.Content.ReadFromJsonAsync<ApiResponse<SongResponse>>();
        var apiResponse2 = await response2.Content.ReadFromJsonAsync<ApiResponse<SongResponse>>();
        
        Assert.NotNull(apiResponse1?.Data);
        Assert.NotNull(apiResponse2?.Data);
        Assert.Contains(apiResponse1.Data.Authors, a => a.Id == _authorId);
        Assert.Contains(apiResponse2.Data.Authors, a => a.Id == _authorId);
    }

    #endregion
}