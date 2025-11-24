public class ApiResponseValue<T> : IApiResponseValue
{
    public T? Message { get; set; }
    public ApiResponseValue(T? message) { Message = message; }
    public object? GetValue() => Message;
}