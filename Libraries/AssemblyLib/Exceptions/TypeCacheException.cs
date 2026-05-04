namespace AssemblyLib.Exceptions;

public class TypeCacheException : Exception
{
    public TypeCacheException(string message)
        : base(message) { }

    public TypeCacheException(string message, Exception innerException)
        : base(message, innerException) { }
}
