namespace Groovo.Exceptions;

public class InvalidRefreshTokenException : Exception
{
    public InvalidRefreshTokenException() : base("Invalid or expired refresh token") {}
    public InvalidRefreshTokenException(string message) : base(message) {}
}