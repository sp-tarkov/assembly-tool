namespace AssemblyLib.Extensions;

public static class StringExtensions
{
    extension(string str)
    {
        public bool IsObfuscatedName()
        {
            var name = str.AsSpan().Trim();
            name = name.TrimStart('_');

            foreach (var prefix in DataProvider.ObfuscatedPrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
