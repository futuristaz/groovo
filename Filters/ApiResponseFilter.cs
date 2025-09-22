using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Groovo.Models;

namespace Groovo.Filters;

public class ApiResponseFilter : IResultFilter
{
  public void OnResultExecuting(ResultExecutingContext context)
  {
    context.Result = WrapResult(context.Result);
  }

  public void OnResultExecuted(ResultExecutedContext context) { }

  private IActionResult WrapResult(IActionResult result)
  {
    switch (result)
    {
      case ObjectResult objResult when IsSerializable(objResult.Value):
        var statusCode = objResult.StatusCode ?? 200;
        objResult.Value = WrapData(objResult.Value, statusCode);
        objResult.StatusCode = statusCode;
        return objResult;

      case CreatedAtActionResult created when IsSerializable(created.Value):
        // Status code 201 is default for CreatedAtAction
        created.Value = WrapData(created.Value, created.StatusCode ?? 201);
        return created;

      // leave untouched
      case NoContentResult:
      case FileResult:
      case IActionResult:
        return result;
    }

    return result;
  }

  private ApiResponse<object> WrapData(object? value, int statusCode)
  {
    var isSuccess = statusCode >= 200 && statusCode < 300;
    ApiError? error = null;

    if (!isSuccess)
    {
      switch (value)
      {
        case string msg:
          error = new ApiError("UNDEFINED_ERROR", msg);
          break;
        case ApiError apiError:
          error = new ApiError(apiError.Code.ToUpperInvariant(), apiError.Message);
          break;
        default:
          error = new ApiError("UNDEFINED_ERROR", value?.ToString() ?? "An error occurred");
          break;
      }
    }

    return new ApiResponse<object>
    {
      Success = isSuccess,
      StatusCode = statusCode,
      Data = isSuccess ? value : null,
      Error = isSuccess ? null : error,
      Meta = new { Timestamp = DateTime.UtcNow }
    };
  }

  private bool IsSerializable(object? value)
  {
    if (value == null) return false;
    if (value is IActionResult) return false;
    if (value is FileResult || value is Stream) return false;
    if (value is ApiResponse<object>) return false;
    return true;
  }
}
