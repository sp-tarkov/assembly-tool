namespace AssemblyLib.Exceptions;

public class DuplicateDirectMapException : Exception
{
    public DuplicateDirectMapException(string message)
        : base(message) { }

    public DuplicateDirectMapException(string message, Exception innerException)
        : base(message, innerException) { }
}
