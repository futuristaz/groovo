namespace Groovo.Exceptions;

public class TokenRevocationException : Exception
{
    public TokenRevocationException() : base("Failed to revoke token") {}
    public TokenRevocationException(string message) : base(message) {}
}
