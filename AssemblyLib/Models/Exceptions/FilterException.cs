namespace AssemblyLib.Models.Exceptions;

public class FilterException : Exception
{
    public FilterException(string message)
        : base(message) { }

    public FilterException(string message, Exception innerException)
        : base(message, innerException) { }
}