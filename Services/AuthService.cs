using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Groovo.Data.Contexts;
using Groovo.Models;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;

namespace Groovo.Services;

public class InternalAuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public interface IAuthService
{
    int AccessTokenExpiryMinutes { get; set; }
    int RefreshTokenExpiryDays { get; set; }

    Task<InternalAuthResponse?> RegisterAsync(RegisterRequest request);
    Task<InternalAuthResponse?> LoginAsync(LoginRequest request);
    Task<InternalAuthResponse?> RefreshTokenAsync(string refreshToken);
    Task<bool> RevokeTokenAsync(string refreshToken);
}

public class AuthService : IAuthService
{
    private const int ACCESS_TOKEN_EXPIRY_MINUTES = 15;
    private const int REFRESH_TOKEN_EXPIRY_DAYS = 30;

    private int _accessTokenExpiryMinutes = ACCESS_TOKEN_EXPIRY_MINUTES;
    private int _refreshTokenExpiryDays = REFRESH_TOKEN_EXPIRY_DAYS;

    public int AccessTokenExpiryMinutes {
        get => _accessTokenExpiryMinutes;
        set => _accessTokenExpiryMinutes = value > 0 ? value : ACCESS_TOKEN_EXPIRY_MINUTES;
    }
    public int RefreshTokenExpiryDays {
        get => _refreshTokenExpiryDays;
        set => _refreshTokenExpiryDays = value > 0 ? value : _refreshTokenExpiryDays;
    }

    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthService> _logger;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(ApplicationDbContext context, IJwtService jwtService, ILogger<AuthService> logger, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
        _passwordHasher = passwordHasher;
    }

    public async Task<InternalAuthResponse?> RegisterAsync(RegisterRequest request)
    {
        try
        {
            // Check if user already exists
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return null;
            }

            // Hash password (first parameter is not used in real implementation)
            var passwordHash = _passwordHasher.HashPassword(null!, request.Password);

            // Create user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Email = request.Email,
                PasswordHash = passwordHash,
                Role = request.Role
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Generate tokens
            var accessToken = _jwtService.GenerateAccessToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();

            // Store refresh token
            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = refreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            };

            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            return new InternalAuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration for email: {Email}", request.Email);
            return null;
        }
    }

    public async Task<InternalAuthResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                return null; // User not found
            }

            // Verify password
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                return null; // Invalid password
            }

            // Generate tokens
            var accessToken = _jwtService.GenerateAccessToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();

            // Store refresh token
            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = refreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            };

            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            return new InternalAuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user login for email: {Email}", request.Email);
            return null;
        }
    }

    public async Task<InternalAuthResponse?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            // Use a database transaction to ensure atomicity
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var storedToken = await _context.RefreshTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.Token == refreshToken && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow);

                if (storedToken == null)
                {
                    return null; // Invalid or expired refresh token
                }

                // Generate new tokens
                var accessToken = _jwtService.GenerateAccessToken(storedToken.User);
                var newRefreshToken = _jwtService.GenerateRefreshToken();

                // Revoke old refresh token immediately
                storedToken.IsRevoked = true;
                
                // Create new refresh token
                var newRefreshTokenEntity = new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    Token = newRefreshToken,
                    UserId = storedToken.UserId,
                    ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays),
                    CreatedAt = DateTime.UtcNow,
                    IsRevoked = false
                };

                _context.RefreshTokens.Add(newRefreshTokenEntity);
                
                // Save both the revocation and new token in one transaction
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new InternalAuthResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = newRefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes)
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return null;
        }
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken)
    {
        try
        {
            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken && !rt.IsRevoked);

            if (storedToken == null)
            {
                return false;
            }

            storedToken.IsRevoked = true;
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token revocation");
            return false;
        }
    }
}