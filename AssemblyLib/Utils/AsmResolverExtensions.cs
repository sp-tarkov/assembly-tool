using AsmResolver;

namespace AssemblyLib.Utils;

internal static class AsmResolverExtensions
{
    public static bool StartsWith(this Utf8String utf8, string value)
    {
        return utf8.ToString().StartsWith(value);
    }

    public static string[] Split(this Utf8String utf8, char separator)
    {
        var str = utf8.ToString();

        return str.Split(separator);
    }
}