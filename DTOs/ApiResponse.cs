using Groovo.DTOs;

namespace Groovo.DTOs;

public class ApiResponse<T>
{
  public bool Success { get; set; }
  public int StatusCode { get; set; }
  public T? Data { get; set; }
  public ApiError[]? Error { get; set; }
  public object Meta { get; set; } = new { Timestamp = DateTime.UtcNow };
}

public record ApiError(string Code, string Message) : IApiResponseValue;