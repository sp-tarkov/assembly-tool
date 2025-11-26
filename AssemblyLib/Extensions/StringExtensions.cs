using AssemblyLib.Shared;

namespace AssemblyLib.Extensions;

public static class StringExtensions
{
    extension(string str)
    {
        public bool IsObfuscatedName()
        {
            if (str.Trim().StartsWith('_'))
            {
                str = str.Replace("_", "");
            }

            var result = DataProvider.ObfuscatedNames.Any(item => str.Contains(item, StringComparison.CurrentCultureIgnoreCase));

            return result;
        }
    }
}
