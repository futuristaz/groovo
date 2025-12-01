using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Groovo.Controllers;
using Groovo.Services;
using Groovo.Models;
using Groovo.DTOs.Responses;
using Groovo.DTOs.Requests;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Groovo.Tests.Controllers;

public class UsersControllerTests
{
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<ILogger<UsersController>> _loggerMock;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _userServiceMock = new Mock<IUserService>();
        _loggerMock = new Mock<ILogger<UsersController>>();
        _controller = new UsersController(_userServiceMock.Object, _loggerMock.Object);
    }

    // ─────────────────────────────────────────────
    // GET /users
    // ─────────────────────────────────────────────
    [Fact]
    public async Task GetAll_ReturnsOkWithUsers()
    {
        // Arrange
        var users = new List<UserSummaryResponse>
        {
            new UserSummaryResponse(Guid.NewGuid(), "John", "john@example.com", UserRole.Author),
            new UserSummaryResponse(Guid.NewGuid(), "Paul", "paul@example.com", UserRole.Author)
        };
        _userServiceMock.Setup(s => s.GetAllUsersAsync(null))
                        .ReturnsAsync(users);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedUsers = Assert.IsAssignableFrom<IEnumerable<UserSummaryResponse>>(okResult.Value);
        Assert.Equal(2, ((List<UserSummaryResponse>)returnedUsers).Count);
    }

    // ─────────────────────────────────────────────
    // GET /users/{id}
    // ─────────────────────────────────────────────
    [Fact]
    public async Task GetById_ReturnsOk_WhenUserExists()
    {
        var id = Guid.NewGuid();
        var userResponse = new UserResponse(id, "John", "Bio", "john@example.com", UserRole.Author, DateTime.UtcNow, DateTime.UtcNow);

        _userServiceMock.Setup(s => s.GetUserByIdAsync(id)).ReturnsAsync(userResponse);

        var result = await _controller.GetById(id);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedUser = Assert.IsType<UserResponse>(okResult.Value);
        Assert.Equal(id, returnedUser.Id);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var id = Guid.NewGuid();
        _userServiceMock.Setup(s => s.GetUserByIdAsync(id)).ReturnsAsync((UserResponse?)null);

        var result = await _controller.GetById(id);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Contains(id.ToString(), notFoundResult.Value!.ToString());
    }

    // ─────────────────────────────────────────────
    // PUT /users/{id}
    // ─────────────────────────────────────────────
    [Fact]
    public async Task Update_ReturnsNoContent_WhenSuccessful()
    {
        var id = Guid.NewGuid();
        var request = new UpdateUserRequest { Name = "Updated", Bio = "Bio", ImageUrl = "img.png" };
        _userServiceMock.Setup(s => s.UpdateUserAsync(id, request.Name, request.Bio, request.ImageUrl))
                        .ReturnsAsync(true);

        var result = await _controller.Update(id, request);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var id = Guid.NewGuid();
        var request = new UpdateUserRequest { Name = "Updated", Bio = "Bio", ImageUrl = "img.png" };
        _userServiceMock.Setup(s => s.UpdateUserAsync(id, request.Name, request.Bio, request.ImageUrl))
                        .ReturnsAsync(false);

        var result = await _controller.Update(id, request);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains(id.ToString(), notFoundResult.Value!.ToString());
    }

    // ─────────────────────────────────────────────
    // DELETE /users/{id}
    // ─────────────────────────────────────────────
    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        var id = Guid.NewGuid();
        _userServiceMock.Setup(s => s.DeleteUserAsync(id)).ReturnsAsync(true);

        var result = await _controller.Delete(id);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var id = Guid.NewGuid();
        _userServiceMock.Setup(s => s.DeleteUserAsync(id)).ReturnsAsync(false);

        var result = await _controller.Delete(id);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains(id.ToString(), notFoundResult.Value!.ToString());
    }

    // ─────────────────────────────────────────────
    // GET /users/search
    // ─────────────────────────────────────────────
    [Fact]
    public async Task Search_ReturnsBadRequest_WhenQueryEmpty()
    {
        var result = await _controller.Search("");
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ─────────────────────────────────────────────
    // GET /users (exception)
    // ─────────────────────────────────────────────
    [Fact]
    public async Task GetAll_ReturnsInternalServerError_OnException()
    {
        _userServiceMock.Setup(s => s.GetAllUsersAsync(null))
                        .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetAll();

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ─────────────────────────────────────────────
    // GET /users/authors
    // ─────────────────────────────────────────────
    [Fact]
    public async Task GetAuthors_ReturnsOk_WithAuthors()
    {
        var authors = new List<UserSummaryResponse>
{
    new UserSummaryResponse(Guid.NewGuid(), "Alice", "alice@example.com", UserRole.Author)
};
        _userServiceMock.Setup(s => s.GetAuthorsAsync()).ReturnsAsync(authors);

        var result = await _controller.GetAuthors();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedAuthors = Assert.IsAssignableFrom<IEnumerable<UserSummaryResponse>>(okResult.Value);
        Assert.Single(returnedAuthors);
    }

    [Fact]
    public async Task GetAuthors_ReturnsInternalServerError_OnException()
    {
        _userServiceMock.Setup(s => s.GetAuthorsAsync()).ThrowsAsync(new Exception("Oops"));

        var result = await _controller.GetAuthors();

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ─────────────────────────────────────────────
    // GET /users/authors/{id}
    // ─────────────────────────────────────────────
    [Fact]
    public async Task GetAuthorById_ReturnsNotFound_WhenAuthorDoesNotExist()
    {
        var id = Guid.NewGuid();
        _userServiceMock.Setup(s => s.GetAuthorByIdAsync(id)).ReturnsAsync((AuthorResponse?)null);

        var result = await _controller.GetAuthorById(id);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Contains(id.ToString(), notFound.Value!.ToString());
    }

    // ─────────────────────────────────────────────
    // GET /users/{id}/playlists - Author forbidden
    // ─────────────────────────────────────────────
    [Fact]
    public async Task GetUserPlaylists_ReturnsForbid_WhenAuthorAccessesOtherUser()
    {
        var authorId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var claims = new List<Claim>
{
    new Claim(ClaimTypes.NameIdentifier, authorId.ToString())
};
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };

        _controller.ControllerContext.HttpContext.User.AddIdentity(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Author") }));

        var result = await _controller.GetUserPlaylists(otherUserId);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal("Authors can only access their own playlists.", objectResult.Value);
    }// ─────────────────────────────────────────────
     // GET /users/{id}/playlists - user not found
     // ─────────────────────────────────────────────
    [Fact]
    public async Task GetUserPlaylists_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
{
    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
};
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };

        _userServiceMock.Setup(s => s.GetUserPlaylistsAsync(userId, true))
                        .ReturnsAsync(new List<PlaylistSummaryResponse>());
        _userServiceMock.Setup(s => s.GetUserByIdAsync(userId)).ReturnsAsync((UserResponse?)null);

        var result = await _controller.GetUserPlaylists(userId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Contains(userId.ToString(), notFound.Value!.ToString());
    }

    [Fact]
    public async Task Search_ReturnsOk_WithResults()
    {
        var query = "John";
        var users = new List<UserSummaryResponse>
        {
            new UserSummaryResponse(Guid.NewGuid(), "John", "john@example.com", UserRole.Author)
        };
        _userServiceMock.Setup(s => s.SearchUsersAsync(query, null)).ReturnsAsync(users);

        var result = await _controller.Search(query);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedUsers = Assert.IsAssignableFrom<IEnumerable<UserSummaryResponse>>(okResult.Value);
        Assert.Single(returnedUsers);
    }
}