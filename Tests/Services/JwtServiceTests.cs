using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Groovo.Models;
using Groovo.Services;
using Groovo.DTOs;

namespace Groovo.Tests.Services;

public class JwtServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly JwtService _jwtService;
    private readonly User _testUser;

    public JwtServiceTests()
    {
        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
            Role = UserRole.User
        };

        var inMemorySettings = new Dictionary<string, string>
        {
            { "JwtSettings:Secret", "super-secret-key-that-is-long-enough-for-256-bits" },
            { "JwtSettings:Issuer", "Groovo" },
            { "JwtSettings:Audience", "Groovo" },
            { "JwtSettings:ExpiryMinutes", "15" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _jwtService = new JwtService(_configuration);
    }

    #region GenerateAccessToken Tests

    [Fact]
    public void GenerateAccessToken_WithValidUser_ReturnsValidToken()
    {
        // Act
        var token = _jwtService.GenerateAccessToken(_testUser);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        Assert.NotNull(jwtToken);
        Assert.Equal("Groovo", jwtToken.Issuer);
        Assert.Equal("Groovo", jwtToken.Audiences.First());
    }

    [Fact]
    public void GenerateAccessToken_TokenContainsUserClaims()
    {
        // Act
        var token = _jwtService.GenerateAccessToken(_testUser);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        Assert.NotNull(jwtToken);

        var claims = jwtToken.Claims.ToList();
        
        Assert.Contains(claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == _testUser.Id.ToString());
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == _testUser.Name);
        Assert.Contains(claims, c => c.Type == ClaimTypes.Email && c.Value == _testUser.Email);
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == _testUser.Role.ToString());
    }

    [Fact]
    public void GenerateAccessToken_TokenContainsJtiClaim()
    {
        // Act
        var token = _jwtService.GenerateAccessToken(_testUser);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        Assert.NotNull(jwtToken);
        var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        
        Assert.NotNull(jtiClaim);
        Assert.True(Guid.TryParse(jtiClaim.Value, out _));
    }

    [Fact]
    public void GenerateAccessToken_TokenContainsIatClaim()
    {
        // Act
        var token = _jwtService.GenerateAccessToken(_testUser);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        Assert.NotNull(jwtToken);
        var iatClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat);
        
        Assert.NotNull(iatClaim);
        Assert.True(long.TryParse(iatClaim.Value, out _));
    }

    [Fact]
    public void GenerateAccessToken_TokenExpiresCorrectly()
    {
        // Act
        var beforeGeneration = DateTime.UtcNow;
        var token = _jwtService.GenerateAccessToken(_testUser);
        var afterGeneration = DateTime.UtcNow;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        Assert.NotNull(jwtToken?.ValidTo);
        
        // ValidTo is truncated to seconds, so we check with tolerance
        var expectedMinExpiry = beforeGeneration.AddMinutes(15);
        var expectedMaxExpiry = afterGeneration.AddMinutes(15).AddSeconds(1);

        Assert.True(jwtToken!.ValidTo >= expectedMinExpiry.AddSeconds(-1) && jwtToken.ValidTo <= expectedMaxExpiry);
    }

    [Fact]
    public void GenerateAccessToken_WithAdminUser_IncludesAdminRole()
    {
        // Arrange
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Name = "Admin User",
            Email = "admin@example.com",
            Role = UserRole.Admin
        };

        // Act
        var token = _jwtService.GenerateAccessToken(adminUser);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        Assert.NotNull(jwtToken);
        Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.Role && c.Value == UserRole.Admin.ToString());
    }

    [Fact]
    public void GenerateAccessToken_WithDifferentExpiryMinutes_RespectsConfiguration()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            { "JwtSettings:Secret", "super-secret-key-that-is-long-enough-for-256-bits" },
            { "JwtSettings:Issuer", "Groovo" },
            { "JwtSettings:Audience", "Groovo" },
            { "JwtSettings:ExpiryMinutes", "60" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var jwtService = new JwtService(configuration);

        // Act
        var beforeGeneration = DateTime.UtcNow;
        var token = jwtService.GenerateAccessToken(_testUser);
        var afterGeneration = DateTime.UtcNow;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        Assert.NotNull(jwtToken?.ValidTo);
        
        // ValidTo is truncated to seconds, so we check with tolerance
        var expectedMinExpiry = beforeGeneration.AddMinutes(60);
        var expectedMaxExpiry = afterGeneration.AddMinutes(60).AddSeconds(1);

        Assert.True(jwtToken!.ValidTo >= expectedMinExpiry.AddSeconds(-1) && jwtToken.ValidTo <= expectedMaxExpiry);
    }

    [Fact]
    public void GenerateAccessToken_WithoutExpiryMinutesInConfig_UsesDefault15()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            { "JwtSettings:Secret", "super-secret-key-that-is-long-enough-for-256-bits" },
            { "JwtSettings:Issuer", "Groovo" },
            { "JwtSettings:Audience", "Groovo" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var jwtService = new JwtService(configuration);

        // Act
        var beforeGeneration = DateTime.UtcNow;
        var token = jwtService.GenerateAccessToken(_testUser);
        var afterGeneration = DateTime.UtcNow;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        // ValidTo is truncated to seconds, so we check with tolerance
        var expectedMinExpiry = beforeGeneration.AddMinutes(15);
        var expectedMaxExpiry = afterGeneration.AddMinutes(15).AddSeconds(1);

        Assert.True(jwtToken!.ValidTo >= expectedMinExpiry.AddSeconds(-1) && jwtToken.ValidTo <= expectedMaxExpiry);
    }

    [Fact]
    public void GenerateAccessToken_WithCustomIssuer_IncludesCustomIssuer()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            { "JwtSettings:Secret", "super-secret-key-that-is-long-enough-for-256-bits" },
            { "JwtSettings:Issuer", "CustomIssuer" },
            { "JwtSettings:Audience", "Groovo" },
            { "JwtSettings:ExpiryMinutes", "15" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var jwtService = new JwtService(configuration);

        // Act
        var token = jwtService.GenerateAccessToken(_testUser);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        Assert.NotNull(jwtToken);
        Assert.Equal("CustomIssuer", jwtToken.Issuer);
    }

    [Fact]
    public void GenerateAccessToken_WithoutIssuerInConfig_UsesDefault()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            { "JwtSettings:Secret", "super-secret-key-that-is-long-enough-for-256-bits" },
            { "JwtSettings:Audience", "Groovo" },
            { "JwtSettings:ExpiryMinutes", "15" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var jwtService = new JwtService(configuration);

        // Act
        var token = jwtService.GenerateAccessToken(_testUser);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        Assert.Equal("Groovo", jwtToken!.Issuer);
    }

    [Fact]
    public void GenerateAccessToken_WithCustomAudience_IncludesCustomAudience()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            { "JwtSettings:Secret", "super-secret-key-that-is-long-enough-for-256-bits" },
            { "JwtSettings:Issuer", "Groovo" },
            { "JwtSettings:Audience", "CustomAudience" },
            { "JwtSettings:ExpiryMinutes", "15" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var jwtService = new JwtService(configuration);

        // Act
        var token = jwtService.GenerateAccessToken(_testUser);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        Assert.NotNull(jwtToken);
        Assert.Contains("CustomAudience", jwtToken.Audiences);
    }

    [Fact]
    public void GenerateAccessToken_WithoutAudienceInConfig_UsesDefault()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            { "JwtSettings:Secret", "super-secret-key-that-is-long-enough-for-256-bits" },
            { "JwtSettings:Issuer", "Groovo" },
            { "JwtSettings:ExpiryMinutes", "15" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var jwtService = new JwtService(configuration);

        // Act
        var token = jwtService.GenerateAccessToken(_testUser);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

        Assert.Contains("Groovo", jwtToken!.Audiences);
    }

    [Fact]
    public void GenerateAccessToken_WithoutSecretInConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            { "JwtSettings:Issuer", "Groovo" },
            { "JwtSettings:Audience", "Groovo" },
            { "JwtSettings:ExpiryMinutes", "15" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var jwtService = new JwtService(configuration);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => jwtService.GenerateAccessToken(_testUser));
    }

    [Fact]
    public void GenerateAccessToken_TokenIsValidWithCorrectSecret()
    {
        // Arrange
        var token = _jwtService.GenerateAccessToken(_testUser);
        var secret = "super-secret-key-that-is-long-enough-for-256-bits";

        // Act
        var handler = new JwtSecurityTokenHandler();
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = "Groovo",
            ValidateIssuer = true,
            ValidIssuer = "Groovo",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = true
        };

        // Assert
        Assert.True(handler.CanReadToken(token));
        var principal = handler.ValidateToken(token, tokenValidationParameters, out _);
        Assert.NotNull(principal);
    }

    #endregion

    #region GenerateRefreshToken Tests

    [Fact]
    public void GenerateRefreshToken_ReturnsValidBase64String()
    {
        // Act
        var refreshToken = _jwtService.GenerateRefreshToken();

        // Assert
        Assert.NotNull(refreshToken);
        Assert.NotEmpty(refreshToken);

        // Should not throw if valid base64
        var decodedBytes = Convert.FromBase64String(refreshToken);
        Assert.NotNull(decodedBytes);
    }

    [Fact]
    public void GenerateRefreshToken_GeneratesRandomTokens()
    {
        // Act
        var refreshToken1 = _jwtService.GenerateRefreshToken();
        var refreshToken2 = _jwtService.GenerateRefreshToken();

        // Assert
        Assert.NotEqual(refreshToken1, refreshToken2);
    }

    [Fact]
    public void GenerateRefreshToken_Token32BytesWhenDecoded()
    {
        // Act
        var refreshToken = _jwtService.GenerateRefreshToken();

        // Assert
        var decodedBytes = Convert.FromBase64String(refreshToken);
        Assert.Equal(32, decodedBytes.Length);
    }

    [Fact]
    public void GenerateRefreshToken_MultipleCallsGenerateDifferentTokens()
    {
        // Act
        var tokens = Enumerable.Range(0, 10)
            .Select(_ => _jwtService.GenerateRefreshToken())
            .ToList();

        // Assert
        // All tokens should be unique
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }

    #endregion

    #region GetPrincipalFromExpiredToken Tests

    [Fact]
    public void GetPrincipalFromExpiredToken_WithExpiredToken_ReturnsPrincipal()
    {
        // Arrange
        var expiredTokenSettings = new Dictionary<string, string>
        {
            { "JwtSettings:Secret", "super-secret-key-that-is-long-enough-for-256-bits" },
            { "JwtSettings:Issuer", "Groovo" },
            { "JwtSettings:Audience", "Groovo" },
            { "JwtSettings:ExpiryMinutes", "-15" } // Negative = already expired
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(expiredTokenSettings!)
            .Build();

        var jwtService = new JwtService(configuration);
        var expiredToken = jwtService.GenerateAccessToken(_testUser);

        // Act
        var principal = jwtService.GetPrincipalFromExpiredToken(expiredToken);

        // Assert
        Assert.NotNull(principal);
        Assert.NotNull(principal.Identity);
        Assert.True(principal.Identity.IsAuthenticated);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ExtractsClaimsCorrectly()
    {
        // Arrange
        var expiredTokenSettings = new Dictionary<string, string>
        {
            { "JwtSettings:Secret", "super-secret-key-that-is-long-enough-for-256-bits" },
            { "JwtSettings:Issuer", "Groovo" },
            { "JwtSettings:Audience", "Groovo" },
            { "JwtSettings:ExpiryMinutes", "-15" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(expiredTokenSettings!)
            .Build();

        var jwtService = new JwtService(configuration);
        var expiredToken = jwtService.GenerateAccessToken(_testUser);

        // Act
        var principal = jwtService.GetPrincipalFromExpiredToken(expiredToken);

        // Assert
        var claims = principal!.Claims.ToList();
        Assert.Contains(claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == _testUser.Id.ToString());
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == _testUser.Name);
        Assert.Contains(claims, c => c.Type == ClaimTypes.Email && c.Value == _testUser.Email);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithValidToken_ReturnsPrincipal()
    {
        // Arrange
        var validToken = _jwtService.GenerateAccessToken(_testUser);

        // Act
        var principal = _jwtService.GetPrincipalFromExpiredToken(validToken);

        // Assert
        Assert.NotNull(principal);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithNullToken_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _jwtService.GetPrincipalFromExpiredToken(null!));
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithEmptyToken_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _jwtService.GetPrincipalFromExpiredToken(string.Empty));
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithoutSecretInConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            { "JwtSettings:Issuer", "Groovo" },
            { "JwtSettings:Audience", "Groovo" },
            { "JwtSettings:ExpiryMinutes", "-15" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var jwtService = new JwtService(configuration);
        var token = _jwtService.GenerateAccessToken(_testUser);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => jwtService.GetPrincipalFromExpiredToken(token));
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithWrongSecret_ThrowsSecurityTokenSignatureKeyNotFoundException()
    {
        // Arrange
        var expiredTokenSettings = new Dictionary<string, string>
        {
            { "JwtSettings:Secret", "super-secret-key-that-is-long-enough-for-256-bits" },
            { "JwtSettings:Issuer", "Groovo" },
            { "JwtSettings:Audience", "Groovo" },
            { "JwtSettings:ExpiryMinutes", "-15" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(expiredTokenSettings!)
            .Build();

        var jwtService = new JwtService(configuration);
        var validToken = jwtService.GenerateAccessToken(_testUser);

        // Now use a different secret
        var wrongSecretSettings = new Dictionary<string, string>
        {
            { "JwtSettings:Secret", "different-secret-key-that-is-long-enough-x" },
            { "JwtSettings:Issuer", "Groovo" },
            { "JwtSettings:Audience", "Groovo" },
            { "JwtSettings:ExpiryMinutes", "-15" }
        };

        var wrongSecretConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(wrongSecretSettings!)
            .Build();

        var jwtServiceWrongSecret = new JwtService(wrongSecretConfig);

        // Act & Assert
        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() => jwtServiceWrongSecret.GetPrincipalFromExpiredToken(validToken));
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WrongTokenFormat_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<SecurityTokenMalformedException>(() => _jwtService.GetPrincipalFromExpiredToken("not.a.jwt.token"));
    }

    #endregion
}
