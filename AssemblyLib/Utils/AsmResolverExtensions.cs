using AsmResolver;

namespace AssemblyLib.Utils;

internal static class AsmResolverExtensions
{
    public static bool StartsWith(this Utf8String utf8, string value)
    {
        return utf8.ToString().StartsWith(value);
    }
}