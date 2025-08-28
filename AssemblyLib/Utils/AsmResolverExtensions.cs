using AsmResolver;

namespace AssemblyLib.Utils;

internal static class AsmResolverExtensions
{
    private static readonly HashSet<string> _typesToMatch =
    [
        "Class",
        "GClass",
        // TODO: Do we even need GControl?
        "GControl",
        "Struct",
        "GStruct",
        "Interface",
        "GInterface",
    ];

    public static bool StartsWith(
        this Utf8String utf8,
        string value,
        StringComparison comparisonType = StringComparison.OrdinalIgnoreCase
    )
    {
        return utf8.ToString().StartsWith(value, comparisonType);
    }

    public static bool EndsWith(
        this Utf8String utf8,
        string value,
        StringComparison comparisonType = StringComparison.OrdinalIgnoreCase
    )
    {
        return utf8.ToString().EndsWith(value, comparisonType);
    }

    public static bool Contains(
        this Utf8String utf8,
        string value,
        StringComparison comparisonType = StringComparison.OrdinalIgnoreCase
    )
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

    /// <summary>
    /// Does the property or field name exist in a given list, this applies prefixes and handles capitalization.
    /// </summary>
    /// <param name="str"></param>
    /// <returns>True if it in the list</returns>
    public static bool IsObfuscatedName(this Utf8String str)
    {
        var realString = str.ToString();

        if (realString.Trim().StartsWith('_'))
        {
            realString = realString.Replace("_", "");
        }

        var result = _typesToMatch.Any(item => realString.StartsWith(item, StringComparison.CurrentCultureIgnoreCase));

        return result;
    }
}
