namespace AssemblyLib.Exceptions;

public class FailedToFindTypeException : Exception
{
    public FailedToFindTypeException(string message)
        : base(message) { }

    public FailedToFindTypeException(string message, Exception innerException)
        : base(message, innerException) { }
}
