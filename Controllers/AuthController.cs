using Microsoft.AspNetCore.Mvc;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Services;

namespace Groovo.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IAuthService authService, ILogger<AuthController> logger, IWebHostEnvironment environment)
    {
        _authService = authService;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>POST: /api/v1/auth/register</summary>
    /// <returns>201 with access token or 400 if registration fails</returns>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _authService.RegisterAsync(request);
            if (result == null)
            {
                return BadRequest("User with this email already exists");
            }

            // Set refresh token in HTTP-only cookie
            ControlRefreshTokenCookie(result.RefreshToken);

            // Remove refresh token from response body for security
            var response = new AuthResponse(
                AccessToken: result.AccessToken,
                ExpiresAt: result.ExpiresAt
            );

            _logger.LogInformation("User registered successfully: {Email}", request.Email);
            return CreatedAtAction(nameof(Register), response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration: {Email}", request.Email);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>POST: /api/v1/auth/login</summary>
    /// <returns>200 with access token or 401 if credentials are invalid</returns>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _authService.LoginAsync(request);
            if (result == null)
            {
                return Unauthorized("Invalid email or password");
            }

            // Set refresh token
            ControlRefreshTokenCookie(result.RefreshToken);

            var response = new AuthResponse(
                AccessToken: result.AccessToken,
                ExpiresAt: result.ExpiresAt
            );

            _logger.LogInformation("User logged in successfully: {Email}", request.Email);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user login: {Email}", request.Email);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>POST: /api/v1/auth/refresh</summary>
    /// <returns>200 with new access token or 401 if refresh token is invalid</returns>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> RefreshToken()
    {
        try
        {
            // Get refresh token
            var refreshToken = GetRefreshTokenFromCookie();
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized("Refresh token not found");
            }

            var result = await _authService.RefreshTokenAsync(refreshToken);
            if (result == null)
            {
                // Clear invalid cookie
                ControlRefreshTokenCookie(setCookie: false);
                return Unauthorized("Invalid or expired refresh token");
            }

            // Set new refresh token
            ControlRefreshTokenCookie(result.RefreshToken);

            // Remove refresh token from response body for security
            var response = new AuthResponse(
                AccessToken: result.AccessToken,
                ExpiresAt: result.ExpiresAt
            );

            _logger.LogInformation("Token refreshed successfully");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>POST: /api/v1/auth/logout</summary>
    /// <returns>200 if logout successful</returns>
    [HttpPost("logout")]
    public async Task<ActionResult> Logout()
    {
        try
        {
            // Get refresh token
            var refreshToken = GetRefreshTokenFromCookie();
            if (!string.IsNullOrEmpty(refreshToken))
            {
                // Revoke the refresh token in the database
                await _authService.RevokeTokenAsync(refreshToken);
            }

            ControlRefreshTokenCookie(setCookie: false);

            _logger.LogInformation("User logged out successfully");
            return Ok("Logged out successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, "Internal server error");
        }
    }

    private void ControlRefreshTokenCookie(string refreshToken = "", DateTime? expires = null, bool setCookie = true)
    {
        if (setCookie)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = !_environment.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Expires = expires ?? DateTime.UtcNow.AddDays(_authService.RefreshTokenExpiryDays),
                Path = "/api/v1/auth"
            };

            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }
        else
        {
            Response.Cookies.Delete("refreshToken");
        }
    }

    private string? GetRefreshTokenFromCookie()
    {
        return Request.Cookies["refreshToken"];
    }
}
