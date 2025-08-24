using AsmResolver;

namespace AssemblyLib.Utils;

internal static class AsmResolverExtensions
{
    public static bool StartsWith(this Utf8String utf8, string value, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
    {
        return utf8.ToString().StartsWith(value, comparisonType);
    }

    public static bool EndsWith(this Utf8String utf8, string value, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
    {
        return utf8.ToString().EndsWith(value, comparisonType);
    }

    public static bool Contains(this Utf8String utf8, string value, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
    {
        return utf8.ToString().Contains(value, comparisonType);
    }

    public static string[] Split(this Utf8String utf8, char separator)
    {
        var str = utf8.ToString();

        return str.Split(separator);
    }
    
    public static Utf8String Replace(this Utf8String utf8, string oldValue, string newValue)
    {
        var str = utf8.ToString();

        return new Utf8String(str.Replace(oldValue, newValue));
    }
}