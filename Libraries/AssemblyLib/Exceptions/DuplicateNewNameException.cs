namespace AssemblyLib.Exceptions;

public class DuplicateNewNameException : Exception
{
    public DuplicateNewNameException(string message)
        : base(message) { }

    public DuplicateNewNameException(string message, Exception innerException)
        : base(message, innerException) { }
}
