using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Moq;
using Groovo.Models;
using Groovo.DTOs.Requests;
using Groovo.Services;
using Groovo.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Groovo.Repositories;
using Groovo.DTOs;

namespace Groovo.Tests;

public class AuthServiceTests
{
    private readonly Mock<IJwtService> _jwtMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly Mock<IPasswordHasher<User>> _hasherMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly AuthService _service;
    
    public AuthServiceTests()
    {
        _jwtMock = new Mock<IJwtService>();
        _loggerMock = new Mock<ILogger<AuthService>>();
        _hasherMock = new Mock<IPasswordHasher<User>>();
        _environmentMock = new Mock<IWebHostEnvironment>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();

        _service = new AuthService(
            _jwtMock.Object, 
            _loggerMock.Object, 
            _hasherMock.Object, 
            _environmentMock.Object,
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object
        );
    }

    [Fact]
    public async Task RegisterAsync_CreatesUser_WhenValid()
    {
        var request = new RegisterRequest
        {
            Name = "Test User",
            Email = "test@example.com",
            Password = "password123",
            Role = UserRole.User
        };

        _userRepositoryMock.Setup(r => r.ExistsAsync(null, request.Email, null))
            .ReturnsAsync(false);

        _hasherMock.Setup(h => h.HashPassword(It.IsAny<User>(), request.Password))
            .Returns("hashedPassword");

        _jwtMock.Setup(j => j.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");
        _jwtMock.Setup(j => j.GenerateRefreshToken()).Returns("refresh-token");

        User capturedUser = null!;
        _userRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u)
            .ReturnsAsync((User u) => u);

        RefreshToken capturedToken = null!;
        _refreshTokenRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(t => capturedToken = t)
            .ReturnsAsync((RefreshToken t) => t);

        var result = await _service.RegisterAsync(request);

        Assert.NotNull(result);
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);

        Assert.NotNull(capturedUser);
        Assert.Equal(request.Email, capturedUser.Email);
        Assert.Equal("hashedPassword", capturedUser.PasswordHash);

        Assert.NotNull(capturedToken);
        Assert.Equal("refresh-token", capturedToken.Token);
        Assert.False(capturedToken.IsRevoked);
    }

    [Fact]
    public async Task RegisterAsync_Throws_WhenUserExists()
    {
        var request = new RegisterRequest { Name = "New User", Email = "existing@example.com", Password = "pass", Role = UserRole.User };

        _userRepositoryMock.Setup(r => r.ExistsAsync(null, request.Email, null))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<UserAlreadyExistsException>(() => _service.RegisterAsync(request));
    }

    [Fact]
    public async Task LoginAsync_ReturnsTokens_WhenValid()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "login@example.com", PasswordHash = "hashed" };

        _userRepositoryMock.Setup(r => r.GetByAsync(null, user.Email))
            .ReturnsAsync(user);

        _hasherMock.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "password"))
            .Returns(PasswordVerificationResult.Success);

        _jwtMock.Setup(j => j.GenerateAccessToken(user)).Returns("access-token");
        _jwtMock.Setup(j => j.GenerateRefreshToken()).Returns("refresh-token");

        RefreshToken capturedToken = null!;
        _refreshTokenRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(t => capturedToken = t)
            .ReturnsAsync((RefreshToken t) => t);

        var request = new LoginRequest { Email = "login@example.com", Password = "password" };
        var result = await _service.LoginAsync(request);

        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);

        Assert.NotNull(capturedToken);
        Assert.Equal(user.Id, capturedToken.UserId);
        Assert.False(capturedToken.IsRevoked);
    }

    [Fact]
    public async Task LoginAsync_Throws_WhenInvalidPassword()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "login@example.com", PasswordHash = "hashed" };

        _userRepositoryMock.Setup(r => r.GetByAsync(null, user.Email))
            .ReturnsAsync(user);

        _hasherMock.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "wrongpass"))
            .Returns(PasswordVerificationResult.Failed);

        var request = new LoginRequest { Email = "login@example.com", Password = "wrongpass" };

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync(request));
    }

    [Fact]
    public async Task RefreshTokenAsync_GeneratesNewTokens()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "refresh@example.com" };
        var oldToken = new RefreshToken 
        { 
            Id = Guid.NewGuid(), 
            Token = "old-token", 
            UserId = user.Id, 
            User = user, 
            ExpiresAt = DateTime.UtcNow.AddDays(1), 
            IsRevoked = false 
        };

        _refreshTokenRepositoryMock.Setup(r => r.GetByTokenAsync("old-token", true, true))
            .ReturnsAsync(oldToken);

        RefreshToken updatedToken = null!;
        _refreshTokenRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(t => updatedToken = t)
            .Returns(Task.CompletedTask);

        RefreshToken newToken = null!;
        _refreshTokenRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(t => newToken = t)
            .ReturnsAsync((RefreshToken t) => t);

        _jwtMock.Setup(j => j.GenerateAccessToken(user)).Returns("new-access");
        _jwtMock.Setup(j => j.GenerateRefreshToken()).Returns("new-refresh");

        var result = await _service.RefreshTokenAsync("old-token");

        Assert.Equal("new-access", result.AccessToken);
        Assert.Equal("new-refresh", result.RefreshToken);

        Assert.NotNull(updatedToken);
        Assert.True(updatedToken.IsRevoked);

        Assert.NotNull(newToken);
        Assert.Equal("new-refresh", newToken.Token);
        Assert.False(newToken.IsRevoked);
    }

    [Fact]
    public async Task RefreshTokenAsync_Throws_WhenInvalidToken()
    {
        _refreshTokenRepositoryMock.Setup(r => r.GetByTokenAsync("invalid", true, true))
            .ReturnsAsync((RefreshToken?)null);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshTokenAsync("invalid"));
    }

    [Fact]
    public async Task RevokeTokenAsync_RevokesToken()
    {
        var token = new RefreshToken { Id = Guid.NewGuid(), Token = "revoke-token", UserId = Guid.NewGuid(), IsRevoked = false };

        _refreshTokenRepositoryMock.Setup(r => r.GetByTokenAsync("revoke-token", false, true))
            .ReturnsAsync(token);

        RefreshToken updatedToken = null!;
        _refreshTokenRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(t => updatedToken = t)
            .Returns(Task.CompletedTask);

        var result = await _service.RevokeTokenAsync("revoke-token");

        Assert.True(result);
        Assert.NotNull(updatedToken);
        Assert.True(updatedToken.IsRevoked);
    }

    [Fact]
    public async Task RevokeTokenAsync_Throws_WhenAlreadyRevoked()
    {
        var token = new RefreshToken { Id = Guid.NewGuid(), Token = "revoked", IsRevoked = true };

        _refreshTokenRepositoryMock.Setup(r => r.GetByTokenAsync("revoked", false, false))
            .ReturnsAsync(token);

        await Assert.ThrowsAsync<TokenRevocationException>(() => _service.RevokeTokenAsync("revoked"));
    }

    #region Profile Settings Tests

    [Fact]
    public async Task UpdateProfileAsync_UpdatesAllFields_WhenAllProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingUser = new User
        {
            Id = userId,
            Name = "Old Name",
            Email = "old@example.com",
            Bio = "Old bio",
            ImageUrl = "old-image.jpg",
            PasswordHash = "hashedPassword"
        };

        _userRepositoryMock.Setup(r => r.GetByAsync(userId, null))
            .ReturnsAsync(existingUser);

        _userRepositoryMock.Setup(r => r.ExistsAsync(null, "new@example.com", userId))
            .ReturnsAsync(false);

        User updatedUser = null!;
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>()))
            .Callback<User>(u => updatedUser = u)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateProfileAsync(userId, "New Name", "New bio", "new-image.jpg", "new@example.com");

        // Assert
        Assert.True(result);
        Assert.NotNull(updatedUser);
        Assert.Equal("New Name", updatedUser.Name);
        Assert.Equal("new@example.com", updatedUser.Email);
        Assert.Equal("New bio", updatedUser.Bio);
        Assert.Equal("new-image.jpg", updatedUser.ImageUrl);
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesOnlyProvidedFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingUser = new User
        {
            Id = userId,
            Name = "Old Name",
            Email = "old@example.com",
            Bio = "Old bio",
            ImageUrl = "old-image.jpg",
            PasswordHash = "hashedPassword"
        };

        _userRepositoryMock.Setup(r => r.GetByAsync(userId, null))
            .ReturnsAsync(existingUser);

        User updatedUser = null!;
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>()))
            .Callback<User>(u => updatedUser = u)
            .Returns(Task.CompletedTask);

        // Act - only update name
        var result = await _service.UpdateProfileAsync(userId, "New Name", null, null, null);

        // Assert
        Assert.True(result);
        Assert.NotNull(updatedUser);
        Assert.Equal("New Name", updatedUser.Name);
        Assert.Equal("old@example.com", updatedUser.Email); // unchanged
        Assert.Equal("Old bio", updatedUser.Bio); // unchanged
        Assert.Equal("old-image.jpg", updatedUser.ImageUrl); // unchanged
    }

    [Fact]
    public async Task UpdateProfileAsync_AllowsEmptyStringForBioAndImageUrl()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingUser = new User
        {
            Id = userId,
            Name = "Name",
            Email = "user@example.com",
            Bio = "Some bio",
            ImageUrl = "some-image.jpg",
            PasswordHash = "hashedPassword"
        };

        _userRepositoryMock.Setup(r => r.GetByAsync(userId, null))
            .ReturnsAsync(existingUser);

        User updatedUser = null!;
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>()))
            .Callback<User>(u => updatedUser = u)
            .Returns(Task.CompletedTask);

        // Act - set bio and imageUrl to empty strings
        var result = await _service.UpdateProfileAsync(userId, null, "", "", null);

        // Assert
        Assert.True(result);
        Assert.NotNull(updatedUser);
        Assert.Equal("", updatedUser.Bio);
        Assert.Equal("", updatedUser.ImageUrl);
    }

    [Fact]
    public async Task UpdateProfileAsync_Throws_WhenEmailAlreadyExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingUser = new User
        {
            Id = userId,
            Name = "Name",
            Email = "user@example.com",
            PasswordHash = "hashedPassword"
        };

        _userRepositoryMock.Setup(r => r.GetByAsync(userId, null))
            .ReturnsAsync(existingUser);

        _userRepositoryMock.Setup(r => r.ExistsAsync(null, "taken@example.com", userId))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() => 
            _service.UpdateProfileAsync(userId, null, null, null, "taken@example.com"));
    }

    [Fact]
    public async Task UpdateProfileAsync_DoesNotCheckEmail_WhenEmailUnchanged()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingUser = new User
        {
            Id = userId,
            Name = "Old Name",
            Email = "user@example.com",
            PasswordHash = "hashedPassword"
        };

        _userRepositoryMock.Setup(r => r.GetByAsync(userId, null))
            .ReturnsAsync(existingUser);

        User updatedUser = null!;
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>()))
            .Callback<User>(u => updatedUser = u)
            .Returns(Task.CompletedTask);

        // Act - same email
        var result = await _service.UpdateProfileAsync(userId, "New Name", null, null, "user@example.com");

        // Assert
        Assert.True(result);
        _userRepositoryMock.Verify(r => r.ExistsAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsFalse_WhenUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock.Setup(r => r.GetByAsync(userId, null))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.UpdateProfileAsync(userId, "Name", null, null, null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ChangePasswordAsync_ChangesPassword_WhenCurrentPasswordCorrect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            PasswordHash = "oldHashedPassword"
        };

        _userRepositoryMock.Setup(r => r.GetByAsync(userId, null))
            .ReturnsAsync(user);

        _hasherMock.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "currentPass"))
            .Returns(PasswordVerificationResult.Success);

        _hasherMock.Setup(h => h.HashPassword(user, "newPass"))
            .Returns("newHashedPassword");

        User updatedUser = null!;
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>()))
            .Callback<User>(u => updatedUser = u)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ChangePasswordAsync(userId, "currentPass", "newPass");

        // Assert
        Assert.True(result);
        Assert.NotNull(updatedUser);
        Assert.Equal("newHashedPassword", updatedUser.PasswordHash);
    }

    [Fact]
    public async Task ChangePasswordAsync_Throws_WhenCurrentPasswordIncorrect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            PasswordHash = "hashedPassword"
        };

        _userRepositoryMock.Setup(r => r.GetByAsync(userId, null))
            .ReturnsAsync(user);

        _hasherMock.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "wrongPass"))
            .Returns(PasswordVerificationResult.Failed);

        // Act & Assert
        await Assert.ThrowsAsync<IncorrectPasswordException>(() => 
            _service.ChangePasswordAsync(userId, "wrongPass", "newPass"));
    }

    [Fact]
    public async Task ChangePasswordAsync_ReturnsFalse_WhenUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock.Setup(r => r.GetByAsync(userId, null))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.ChangePasswordAsync(userId, "currentPass", "newPass");

        // Assert
        Assert.False(result);
    }

    #endregion
}
