namespace Groovo.Exceptions;

public class IncorrectPasswordException : Exception
{
    public IncorrectPasswordException() 
        : base("Current password is incorrect")
    {
    }
}
