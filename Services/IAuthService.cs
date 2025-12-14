using Groovo.DTOs.InternalResponses;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Microsoft.AspNetCore.Http;

namespace Groovo.Services;

public interface IAuthService
{
    int AccessTokenExpiryMinutes { get; set; }
    int RefreshTokenExpiryDays { get; set; }

    Task<InternalAuthResponse> RegisterAsync(RegisterRequest request);
    Task<InternalAuthResponse> LoginAsync(LoginRequest request);
    Task<InternalAuthResponse> RefreshTokenAsync(string refreshToken);
    Task<bool> RevokeTokenAsync(string refreshToken);
    Task<bool> UpdateProfileAsync(Guid userId, string? name, string? bio, string? imageUrl, string? email);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    void SetRefreshTokenCookie(HttpResponse response, string refreshToken, DateTime? expires = null);
    void ClearRefreshTokenCookie(HttpResponse response);
    string? GetRefreshTokenFromCookie(HttpRequest request);
}