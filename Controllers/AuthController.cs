using Microsoft.AspNetCore.Mvc;
using Groovo.DTOs.Requests;
using Groovo.DTOs.Responses;
using Groovo.Services;
using Groovo.Exceptions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Groovo.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IUserService userService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _userService = userService;
        _logger = logger;
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

            _authService.SetRefreshTokenCookie(Response, result.RefreshToken);

            var response = new AuthResponse(
                AccessToken: result.AccessToken,
                ExpiresAt: result.ExpiresAt
            );

            _logger.LogInformation("User registered successfully: {Email}", request.Email);
            return CreatedAtAction(nameof(Register), response);
        }
        catch (UserAlreadyExistsException ex)
        {
            return BadRequest(ex.Message);
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

            _authService.SetRefreshTokenCookie(Response, result.RefreshToken);

            var response = new AuthResponse(
                AccessToken: result.AccessToken,
                ExpiresAt: result.ExpiresAt
            );

            _logger.LogInformation("User logged in successfully: {Email}", request.Email);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(ex.Message);
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
            var refreshToken = _authService.GetRefreshTokenFromCookie(Request);
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized("Refresh token not found");
            }

            var result = await _authService.RefreshTokenAsync(refreshToken);

            _authService.SetRefreshTokenCookie(Response, result.RefreshToken);

            var response = new AuthResponse(
                AccessToken: result.AccessToken,
                ExpiresAt: result.ExpiresAt
            );

            _logger.LogInformation("Token refreshed successfully");
            return Ok(response);
        }
        catch (InvalidRefreshTokenException ex)
        {
            _authService.ClearRefreshTokenCookie(Response);
            return Unauthorized(ex.Message);
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
            var refreshToken = _authService.GetRefreshTokenFromCookie(Request);
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _authService.RevokeTokenAsync(refreshToken);
            }

            _authService.ClearRefreshTokenCookie(Response);

            _logger.LogInformation("User logged out successfully");
            return Ok("Logged out successfully");
        }
        catch (TokenRevocationException)
        {
            // Still clear the cookie even if revocation failed
            _authService.ClearRefreshTokenCookie(Response);
            return Ok("Logged out successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>GET: /api/v1/auth/me</summary>
    /// <returns>200 with current user information or 401 if not authenticated</returns>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> GetCurrentUser()
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Invalid user token");
            }

            var user = await _userService.GetUserByIdAsync(userId);
            
            if (user == null)
            {
                return NotFound("User not found");
            }

            _logger.LogInformation("Retrieved current user information: {UserId}", userId);
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user information");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>PUT: /api/v1/auth/me</summary>
    /// <returns>204 if successful, 400 if validation fails, 409 if email already exists</returns>
    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
            var success = await _authService.UpdateProfileAsync(userId, request.Name, request.Bio, request.ImageUrl, request.Email);
            
            if (!success)
                return NotFound("User not found.");

            _logger.LogInformation("Profile updated successfully for user {UserId}", userId);
            return NoContent();
        }
        catch (EmailAlreadyExistsException ex)
        {
            _logger.LogWarning(ex, "Profile update failed - email already exists");
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>PUT: /api/v1/auth/me/password</summary>
    /// <returns>204 if successful, 400 if current password is incorrect</returns>
    [HttpPut("me/password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
            var success = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            
            if (!success)
                return NotFound("User not found.");

            _logger.LogInformation("Password changed successfully for user {UserId}", userId);
            return NoContent();
        }
        catch (IncorrectPasswordException ex)
        {
            _logger.LogWarning(ex, "Password change failed - incorrect current password");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return StatusCode(500, "Internal server error");
        }
    }
}
