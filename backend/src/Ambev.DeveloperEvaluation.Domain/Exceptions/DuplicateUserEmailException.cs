namespace Ambev.DeveloperEvaluation.Domain.Exceptions;

public sealed class DuplicateUserEmailException : Exception
{
    public DuplicateUserEmailException(string email, Exception? innerException = null)
        : base($"User with email {email} already exists", innerException)
    {
    }
}
