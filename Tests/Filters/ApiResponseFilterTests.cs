using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Groovo.DTOs;
using Groovo.Filters;

namespace Groovo.Tests.Filters;

public class ApiResponseFilterTests
{
    private readonly ApiResponseFilter _filter;

    public ApiResponseFilterTests()
    {
        _filter = new ApiResponseFilter();
    }

    private static ControllerActionDescriptor CreateActionDescriptor()
    {
        return new ControllerActionDescriptor();
    }

    #region OnResultExecuting Tests

    [Fact]
    public void OnResultExecuting_WithObjectResult_WrapsResult()
    {
        // Arrange
        var testData = new { Name = "Test" };
        var objResult = new OkObjectResult(testData);
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        Assert.NotNull(mockContext.Result);
        Assert.IsType<OkObjectResult>(mockContext.Result);
        var result = mockContext.Result as OkObjectResult;
        Assert.IsType<ApiResponse<object>>(result!.Value);
        var apiResponse = result.Value as ApiResponse<object>;
        Assert.True(apiResponse!.Success);
        Assert.Equal(200, apiResponse.StatusCode);
    }

    [Fact]
    public void OnResultExecuting_WithNoContentResult_LeavesUntouched()
    {
        // Arrange
        var noContentResult = new NoContentResult();
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            noContentResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        Assert.Same(noContentResult, mockContext.Result);
    }

    [Fact]
    public void OnResultExecuting_WithFileResult_LeavesUntouched()
    {
        // Arrange
        var fileResult = new FileContentResult(new byte[] { }, "application/json");
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            fileResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        Assert.Same(fileResult, mockContext.Result);
    }

    [Fact]
    public void OnResultExecuting_WithAlreadyWrappedApiResponse_SkipsWrapping()
    {
        // Arrange
        var apiResponse = new ApiResponse<object>
        {
            Success = true,
            StatusCode = 200,
            Data = new { Test = "data" }
        };
        var objResult = new OkObjectResult(apiResponse);
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        Assert.Same(objResult, mockContext.Result);
    }

    #endregion

    #region OnResultExecuted Tests

    [Fact]
    public void OnResultExecuted_DoesNothing()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutedContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            new OkResult(),
            new object()
        );

        // Act & Assert - should not throw
        _filter.OnResultExecuted(mockContext);
    }

    #endregion

    #region WrapResult - ObjectResult Branch Tests

    [Fact]
    public void WrapResult_ObjectResult_Success_200_StatusCode()
    {
        // Arrange
        var testData = new { Name = "Test", Value = 123 };
        var objResult = new ObjectResult(testData) { StatusCode = 200 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.True(apiResponse!.Success);
        Assert.Equal(200, apiResponse.StatusCode);
        Assert.NotNull(apiResponse.Data);
        Assert.Empty(apiResponse.Error!);
    }

    [Fact]
    public void WrapResult_ObjectResult_Success_201_StatusCode()
    {
        // Arrange
        var testData = new { Id = 1 };
        var objResult = new ObjectResult(testData) { StatusCode = 201 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.True(apiResponse!.Success);
        Assert.Equal(201, apiResponse.StatusCode);
    }

    [Fact]
    public void WrapResult_ObjectResult_Success_299_StatusCode()
    {
        // Arrange
        var testData = new { Success = true };
        var objResult = new ObjectResult(testData) { StatusCode = 299 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.True(apiResponse!.Success);
        Assert.Equal(299, apiResponse.StatusCode);
    }

    [Fact]
    public void WrapResult_ObjectResult_DefaultStatusCode_200()
    {
        // Arrange
        var testData = new { Default = "status code" };
        var objResult = new ObjectResult(testData);
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.True(apiResponse!.Success);
        Assert.Equal(200, apiResponse.StatusCode);
    }

    [Fact]
    public void WrapResult_ObjectResult_AlreadyIApiResponseValue_DoesNotWrapTwice()
    {
        // Arrange
        var responseValue = new ApiResponseValue<string>("Already wrapped");
        var objResult = new ObjectResult(responseValue) { StatusCode = 200 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.NotNull(apiResponse);
    }

    #endregion

    #region WrapResult - CreatedAtActionResult Branch Tests

    [Fact]
    public void WrapResult_CreatedAtActionResult_With201StatusCode()
    {
        // Arrange
        var testData = new { Id = 1, Name = "New Item" };
        var createdResult = new CreatedAtActionResult("GetItem", "Controller", new { id = 1 }, testData)
        {
            StatusCode = 201
        };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            createdResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as CreatedAtActionResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.True(apiResponse!.Success);
        Assert.Equal(201, apiResponse.StatusCode);
    }

    [Fact]
    public void WrapResult_CreatedAtActionResult_DefaultStatusCode()
    {
        // Arrange
        var testData = new { Id = 2 };
        var createdResult = new CreatedAtActionResult("GetItem", "Controller", new { id = 2 }, testData);
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            createdResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as CreatedAtActionResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.Equal(201, apiResponse!.StatusCode);
    }

    [Fact]
    public void WrapResult_CreatedAtActionResult_AlreadyIApiResponseValue()
    {
        // Arrange
        var responseValue = new ApiResponseValue<string>("Created");
        var createdResult = new CreatedAtActionResult("GetItem", "Controller", new { id = 1 }, responseValue);
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            createdResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as CreatedAtActionResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.NotNull(apiResponse);
    }

    #endregion

    #region WrapData - Error Handling - String Message Tests

    [Fact]
    public void WrapData_ErrorStatusCode_WithStringMessage_CreatesValidationError()
    {
        // Arrange
        var errorMessage = "User not found";
        var responseValue = new ApiResponseValue<string>(errorMessage);
        var objResult = new ObjectResult(responseValue) { StatusCode = 404 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.False(apiResponse!.Success);
        Assert.Equal(404, apiResponse.StatusCode);
        Assert.Null(apiResponse.Data);
        Assert.Single(apiResponse.Error!);
        Assert.Equal("UNDEFINED_ERROR", apiResponse.Error![0].Code);
        Assert.Equal(errorMessage, apiResponse.Error![0].Message);
    }

    [Fact]
    public void WrapData_ErrorStatusCode_400_WithStringMessage()
    {
        // Arrange
        var objResult = new ObjectResult(new ApiResponseValue<string>("Bad request")) { StatusCode = 400 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.False(apiResponse!.Success);
    }

    [Fact]
    public void WrapData_ErrorStatusCode_500_WithStringMessage()
    {
        // Arrange
        var objResult = new ObjectResult(new ApiResponseValue<string>("Internal server error")) { StatusCode = 500 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.False(apiResponse!.Success);
        Assert.Equal(500, apiResponse.StatusCode);
    }

    #endregion

    #region WrapData - Error Handling - ApiError Tests

    [Fact]
    public void WrapData_ErrorStatusCode_WithApiError_FormatsErrorCorrectly()
    {
        // Arrange
        var apiError = new ApiError("CUSTOM_ERROR", "Custom error message");
        var responseValue = new ApiResponseValue<ApiError>(apiError);
        var objResult = new ObjectResult(responseValue) { StatusCode = 422 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.False(apiResponse!.Success);
        Assert.Single(apiResponse.Error!);
        Assert.Equal("CUSTOM_ERROR", apiResponse.Error![0].Code);
        Assert.Equal("Custom error message", apiResponse.Error![0].Message);
    }

    [Fact]
    public void WrapData_ErrorStatusCode_WithApiError_ConvertsCodeToUppercase()
    {
        // Arrange
        var apiError = new ApiError("lowercase_error", "Error message");
        var responseValue = new ApiResponseValue<ApiError>(apiError);
        var objResult = new ObjectResult(responseValue) { StatusCode = 422 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.Equal("LOWERCASE_ERROR", apiResponse!.Error![0].Code);
    }

    #endregion

    #region WrapData - Error Handling - ValidationProblemDetails Tests

    [Fact]
    public void WrapData_ErrorStatusCode_WithValidationProblemDetails_WithErrors()
    {
        // Arrange
        var problemDetails = new ValidationProblemDetails
        {
            Errors = new Dictionary<string, string[]>
            {
                { "Email", new[] { "Invalid email format" } },
                { "Password", new[] { "Password too short" } }
            }
        };
        var responseValue = new ApiResponseValue<ValidationProblemDetails>(problemDetails);
        var objResult = new ObjectResult(responseValue) { StatusCode = 400 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.False(apiResponse!.Success);
        Assert.Equal(2, apiResponse.Error!.Length);
        Assert.All(apiResponse.Error!, error => Assert.Equal("VALIDATION_ERROR", error.Code));
    }

    [Fact]
    public void WrapData_ErrorStatusCode_WithValidationProblemDetails_EmptyErrors()
    {
        // Arrange
        var problemDetails = new ValidationProblemDetails
        {
            Errors = new Dictionary<string, string[]>()
        };
        var responseValue = new ApiResponseValue<ValidationProblemDetails>(problemDetails);
        var objResult = new ObjectResult(responseValue) { StatusCode = 400 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.False(apiResponse!.Success);
        Assert.Single(apiResponse.Error!);
        Assert.Equal("VALIDATION_ERROR", apiResponse.Error![0].Code);
        Assert.Equal("Validation error occurred", apiResponse.Error![0].Message);
    }

    [Fact]
    public void WrapData_ErrorStatusCode_WithValidationProblemDetails_NullErrors()
    {
        // Arrange
        var problemDetails = new ValidationProblemDetails
        {
            Errors = new Dictionary<string, string[]>()
        };
        var responseValue = new ApiResponseValue<ValidationProblemDetails>(problemDetails);
        var objResult = new ObjectResult(responseValue) { StatusCode = 400 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.False(apiResponse!.Success);
        Assert.Single(apiResponse.Error!);
        Assert.Equal("VALIDATION_ERROR", apiResponse.Error![0].Code);
    }

    [Fact]
    public void WrapData_ErrorStatusCode_WithValidationProblemDetails_MultipleErrorsPerField()
    {
        // Arrange
        var problemDetails = new ValidationProblemDetails
        {
            Errors = new Dictionary<string, string[]>
            {
                { "Field1", new[] { "Error 1", "Error 2", "Error 3" } }
            }
        };
        var responseValue = new ApiResponseValue<ValidationProblemDetails>(problemDetails);
        var objResult = new ObjectResult(responseValue) { StatusCode = 400 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        // Should take first error
        Assert.NotNull(apiResponse!.Error);
        Assert.Single(apiResponse.Error!);
        Assert.Equal("Error 1", apiResponse.Error![0].Message);
    }

    [Fact]
    public void WrapData_ErrorStatusCode_WithValidationProblemDetails_NullErrorValue()
    {
        // Arrange
        var problemDetails = new ValidationProblemDetails
        {
            Errors = new Dictionary<string, string[]>
            {
                { "Field1", Array.Empty<string>() }
            }
        };
        var responseValue = new ApiResponseValue<ValidationProblemDetails>(problemDetails);
        var objResult = new ObjectResult(responseValue) { StatusCode = 400 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.NotNull(apiResponse!.Error);
        Assert.Single(apiResponse.Error!);
        Assert.Equal("Validation error occurred", apiResponse.Error![0].Message);
    }

    #endregion

    #region WrapData - Error Handling - Default Case Tests

    [Fact]
    public void WrapData_ErrorStatusCode_WithUnrecognizedObjectType_UsesToString()
    {
        // Arrange
        var customObject = new { CustomField = "Custom Value" };
        var responseValue = new ApiResponseValue<object>(customObject);
        var objResult = new ObjectResult(responseValue) { StatusCode = 409 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.False(apiResponse!.Success);
        Assert.Single(apiResponse.Error!);
        Assert.Equal("UNDEFINED_ERROR", apiResponse.Error![0].Code);
    }

    [Fact]
    public void WrapData_ErrorStatusCode_WithNullValue_HandlesGracefully()
    {
        // Arrange
        var responseValue = new ApiResponseValue<object>(null);
        var objResult = new ObjectResult(responseValue) { StatusCode = 500 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.False(apiResponse!.Success);
        Assert.Single(apiResponse.Error!);
        Assert.Equal("UNDEFINED_ERROR", apiResponse.Error![0].Code);
    }

    #endregion

    #region IsSerializable Tests

    [Fact]
    public void WrapResult_WithNullValue_IsNotSerializable()
    {
        // Arrange
        var objResult = new ObjectResult(null);
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        Assert.Same(objResult, mockContext.Result);
    }

    [Fact]
    public void WrapResult_WithIActionResult_IsNotSerializable()
    {
        // Arrange
        var badResult = new BadRequestResult() as IActionResult;
        var objResult = new ObjectResult(badResult);
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        Assert.Same(objResult, mockContext.Result);
    }

    [Fact]
    public void WrapResult_WithStream_IsNotSerializable()
    {
        // Arrange
        using (var stream = new MemoryStream())
        {
            var objResult = new ObjectResult(stream);
            var httpContext = new DefaultHttpContext();
            var routeData = new RouteData();
            var mockContext = new ResultExecutingContext(
                new ActionContext(httpContext, routeData, CreateActionDescriptor()),
                new List<IFilterMetadata>(),
                objResult,
                new object()
            );

            // Act
            _filter.OnResultExecuting(mockContext);

            // Assert
            Assert.Same(objResult, mockContext.Result);
        }
    }

    [Fact]
    public void WrapResult_WithApiResponse_IsNotSerializable()
    {
        // Arrange
        var apiResponse = new ApiResponse<object> { Success = true };
        var objResult = new ObjectResult(apiResponse);
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        Assert.Same(objResult, mockContext.Result);
    }

    #endregion

    #region Meta Timestamp Tests

    [Fact]
    public void WrapData_IncludesMeta_WithCurrentTimestamp()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;
        var testData = new { Data = "Test" };
        var objResult = new OkObjectResult(testData);
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);
        var afterCreation = DateTime.UtcNow;

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.NotNull(apiResponse!.Meta);
    }

    #endregion

    #region Edge Cases and Boundary Conditions

    [Fact]
    public void WrapResult_ObjectResult_StatusCode199_IsNotSuccess()
    {
        // Arrange
        var testData = new { Test = "data" };
        var objResult = new ObjectResult(testData) { StatusCode = 199 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.False(apiResponse!.Success);
    }

    [Fact]
    public void WrapResult_ObjectResult_StatusCode300_IsNotSuccess()
    {
        // Arrange
        var testData = new { Test = "data" };
        var objResult = new ObjectResult(testData) { StatusCode = 300 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.False(apiResponse!.Success);
    }

    [Fact]
    public void WrapData_SuccessResponse_DataIsPopulated()
    {
        // Arrange
        var expectedData = new { Id = 123, Name = "Test Item" };
        var objResult = new OkObjectResult(expectedData);
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.True(apiResponse!.Success);
        Assert.NotNull(apiResponse.Data);
    }

    [Fact]
    public void WrapData_ErrorResponse_DataIsNull()
    {
        // Arrange
        var objResult = new ObjectResult(new ApiResponseValue<string>("Error")) { StatusCode = 400 };
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.False(apiResponse!.Success);
        Assert.Null(apiResponse.Data);
    }

    [Fact]
    public void WrapData_SuccessResponse_ErrorArrayIsEmpty()
    {
        // Arrange
        var objResult = new OkObjectResult(new { Success = true });
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var mockContext = new ResultExecutingContext(
            new ActionContext(httpContext, routeData, CreateActionDescriptor()),
            new List<IFilterMetadata>(),
            objResult,
            new object()
        );

        // Act
        _filter.OnResultExecuting(mockContext);

        // Assert
        var result = mockContext.Result as ObjectResult;
        var apiResponse = result!.Value as ApiResponse<object>;
        Assert.True(apiResponse!.Success);
        Assert.Empty(apiResponse.Error!);
    }

    #endregion
}
