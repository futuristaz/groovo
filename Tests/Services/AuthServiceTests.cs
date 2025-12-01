using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;
using Groovo.Data.Contexts;
using Groovo.Models;
using Groovo.DTOs.Requests;
using Groovo.DTOs.InternalResponses;
using Groovo.Services;
using Groovo.Exceptions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;

namespace Groovo.Tests;

public class AuthServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IJwtService> _jwtMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly Mock<IPasswordHasher<User>> _hasherMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly AuthService _service;
    
    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _jwtMock = new Mock<IJwtService>();
        _loggerMock = new Mock<ILogger<AuthService>>();
        _hasherMock = new Mock<IPasswordHasher<User>>();
        _environmentMock = new Mock<IWebHostEnvironment>();

        _service = new AuthService(_context, _jwtMock.Object, _loggerMock.Object, _hasherMock.Object, _environmentMock.Object);
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

        _hasherMock.Setup(h => h.HashPassword(It.IsAny<User>(), request.Password))
                    .Returns("hashedPassword");

        _jwtMock.Setup(j => j.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");
        _jwtMock.Setup(j => j.GenerateRefreshToken()).Returns("refresh-token");

        var result = await _service.RegisterAsync(request);

        Assert.NotNull(result);
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);

        var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        Assert.NotNull(userInDb);
        Assert.Equal("hashedPassword", userInDb.PasswordHash);

        var refreshTokenInDb = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.UserId == userInDb.Id);
        Assert.NotNull(refreshTokenInDb);
        Assert.False(refreshTokenInDb.IsRevoked);
    }

    [Fact]
    public async Task RegisterAsync_Throws_WhenUserExists()
    {
        var existingUser = new User { Id = Guid.NewGuid(), Email = "existing@example.com" };
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        var request = new RegisterRequest { Name = "New User", Email = "existing@example.com", Password = "pass", Role = UserRole.User };

        await Assert.ThrowsAsync<UserAlreadyExistsException>(() => _service.RegisterAsync(request));
    }

    [Fact]
    public async Task LoginAsync_ReturnsTokens_WhenValid()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "login@example.com", PasswordHash = "hashed" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _hasherMock.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "password"))
                    .Returns(PasswordVerificationResult.Success);

        _jwtMock.Setup(j => j.GenerateAccessToken(user)).Returns("access-token");
        _jwtMock.Setup(j => j.GenerateRefreshToken()).Returns("refresh-token");

        var request = new LoginRequest { Email = "login@example.com", Password = "password" };
        var result = await _service.LoginAsync(request);

        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);

        var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.UserId == user.Id);
        Assert.NotNull(storedToken);
        Assert.False(storedToken.IsRevoked);
    }

    [Fact]
    public async Task LoginAsync_Throws_WhenInvalidPassword()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "login@example.com", PasswordHash = "hashed" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _hasherMock.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash, "wrongpass"))
                    .Returns(PasswordVerificationResult.Failed);

        var request = new LoginRequest { Email = "login@example.com", Password = "wrongpass" };

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync(request));
    }

    [Fact]
    public async Task RefreshTokenAsync_GeneratesNewTokens()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "refresh@example.com" };
        var oldToken = new RefreshToken { Id = Guid.NewGuid(), Token = "old-token", UserId = user.Id, User = user, ExpiresAt = DateTime.UtcNow.AddDays(1), IsRevoked = false };
        _context.Users.Add(user);
        _context.RefreshTokens.Add(oldToken);
        await _context.SaveChangesAsync();

        _jwtMock.Setup(j => j.GenerateAccessToken(user)).Returns("new-access");
        _jwtMock.Setup(j => j.GenerateRefreshToken()).Returns("new-refresh");

        var result = await _service.RefreshTokenAsync("old-token");

        Assert.Equal("new-access", result.AccessToken);
        Assert.Equal("new-refresh", result.RefreshToken);

        var oldTokenDb = await _context.RefreshTokens.FirstAsync(rt => rt.Id == oldToken.Id);
        Assert.True(oldTokenDb.IsRevoked);

        var newTokenDb = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == "new-refresh");
        Assert.NotNull(newTokenDb);
    }

    [Fact]
    public async Task RefreshTokenAsync_Throws_WhenInvalidToken()
    {
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshTokenAsync("invalid"));
    }

    [Fact]
    public async Task RevokeTokenAsync_RevokesToken()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "revoke@example.com" };
        var token = new RefreshToken { Id = Guid.NewGuid(), Token = "revoke-token", UserId = user.Id, IsRevoked = false };
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();

        var result = await _service.RevokeTokenAsync("revoke-token");

        Assert.True(result);

        var tokenDb = await _context.RefreshTokens.FirstAsync(rt => rt.Id == token.Id);
        Assert.True(tokenDb.IsRevoked);
    }

    [Fact]
    public async Task RevokeTokenAsync_Throws_WhenAlreadyRevoked()
    {
        var token = new RefreshToken { Id = Guid.NewGuid(), Token = "revoked", IsRevoked = true };
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<TokenRevocationException>(() => _service.RevokeTokenAsync("revoked"));
    }
}