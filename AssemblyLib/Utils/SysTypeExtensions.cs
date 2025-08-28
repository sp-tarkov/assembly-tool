using System.Text;
using AsmResolver;

namespace AssemblyLib.Utils;

public static class SysTypeExtensions
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
