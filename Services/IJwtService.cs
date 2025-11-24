using System.Security.Claims;
using Groovo.Models;

namespace Groovo.Services; 

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}