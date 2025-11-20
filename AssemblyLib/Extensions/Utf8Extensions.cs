using AsmResolver;

namespace AssemblyLib.Extensions;

internal static class Utf8Extensions
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

    /// <param name="utf8"></param>
    extension(Utf8String utf8)
    {
        public bool StartsWith(string value,
            StringComparison comparisonType = StringComparison.OrdinalIgnoreCase
        )
        {
            return utf8.ToString().StartsWith(value, comparisonType);
        }

        public bool EndsWith(string value,
            StringComparison comparisonType = StringComparison.OrdinalIgnoreCase
        )
        {
            return utf8.ToString().EndsWith(value, comparisonType);
        }

        public bool Contains(string value,
            StringComparison comparisonType = StringComparison.OrdinalIgnoreCase
        )
        {
            return utf8.ToString().Contains(value, comparisonType);
        }

        public string[] Split(char separator)
        {
            var str = utf8.ToString();

            return str.Split(separator);
        }

        public Utf8String Replace(string oldValue, string newValue)
        {
            var str = utf8.ToString();

            return new Utf8String(str.Replace(oldValue, newValue));
        }

        /// <summary>
        /// Does the property or field name exist in a given list, this applies prefixes and handles capitalization.
        /// </summary>
        /// <returns>True if it in the list</returns>
        public bool IsObfuscatedName()
        {
            var realString = utf8.ToString();

            if (realString.Trim().StartsWith('_'))
            {
                realString = realString.Replace("_", "");
            }

            var result = _typesToMatch.Any(item => realString.StartsWith(item, StringComparison.CurrentCultureIgnoreCase));

            return result;
        }
    }
}
