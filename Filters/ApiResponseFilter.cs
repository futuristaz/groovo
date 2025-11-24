using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Groovo.DTOs;

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
                if (objResult.Value is not IApiResponseValue)
                {
                    objResult.Value = new ApiResponseValue<object>(objResult.Value);
                }
                objResult.Value = WrapData((IApiResponseValue)objResult.Value, statusCode);
                objResult.StatusCode = statusCode;
                return objResult;

            case CreatedAtActionResult created when IsSerializable(created.Value):
                // Status code 201 is default for CreatedAtAction
                if (created.Value is not IApiResponseValue)
                {
                    created.Value = new ApiResponseValue<object>(created.Value);
                }
                created.Value = WrapData((IApiResponseValue)created.Value, created.StatusCode ?? 201);
                return created;

            // leave untouched
            case NoContentResult:
            case FileResult:
            case IActionResult:
                return result;
        }

        return result;
    }

    private ApiResponse<object> WrapData<T>(T value, int statusCode)
      where T : class, IApiResponseValue
    {
        bool isSuccess = statusCode >= 200 && statusCode < 300;
        ApiError[] error = Array.Empty<ApiError>();

        if (!isSuccess)
        {
            var valueObj = value.GetValue();
            switch (valueObj)
            {
                case string msg:
                    error = [new ApiError("UNDEFINED_ERROR", msg)];
                    break;
                case ApiError apiError:
                    error = [new ApiError(apiError.Code.ToUpperInvariant(), apiError.Message)];
                    break;
                case ValidationProblemDetails problemDetails:
                    if (problemDetails.Errors != null && problemDetails.Errors.Count > 0)
                    {
                        error = problemDetails.Errors
                          .Select(kvp => new ApiError("VALIDATION_ERROR", kvp.Value?.FirstOrDefault() ?? "Validation error occurred"))
                          .ToArray();
                    }
                    else
                    {
                        error = [new ApiError("VALIDATION_ERROR", "Validation error occurred")];
                    }
                    break;
                default:
                    error = [new ApiError("UNDEFINED_ERROR", value?.ToString() ?? "An error occurred")];
                    break;
            }
        }

        return new ApiResponse<object>
        {
            Success = isSuccess,
            StatusCode = statusCode,
            Data = isSuccess ? value?.GetValue() : null,
            Error = isSuccess ? Array.Empty<ApiError>() : error,
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
