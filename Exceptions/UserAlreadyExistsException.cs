namespace Groovo.Exceptions;

public class UserAlreadyExistsException : Exception
{
    public string Email { get; }
    public UserAlreadyExistsException(string email) : base($"User with email '{email}' already exists")
    {
      Email = email;
    }
}