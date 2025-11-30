using Groovo.DTOs.InternalResponses;
using Groovo.DTOs.Requests;
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
    void SetRefreshTokenCookie(HttpResponse response, string refreshToken, DateTime? expires = null);
    void ClearRefreshTokenCookie(HttpResponse response);
    string? GetRefreshTokenFromCookie(HttpRequest request);
}