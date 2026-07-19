using AsmResolver.DotNet.Signatures;

namespace AssemblyLib.Extensions;

public static class StringExtensions
{
    /// <summary>
    ///     CLR type names mapped to the C# keyword the deobfuscator uses when it auto names a member.
    /// </summary>
    private static readonly Dictionary<string, string> _keywordAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Boolean"] = "bool",
        ["Byte"] = "byte",
        ["SByte"] = "sbyte",
        ["Char"] = "char",
        ["Decimal"] = "decimal",
        ["Double"] = "double",
        ["Single"] = "float",
        ["Int16"] = "short",
        ["UInt16"] = "ushort",
        ["Int32"] = "int",
        ["UInt32"] = "uint",
        ["Int64"] = "long",
        ["UInt64"] = "ulong",
        ["Object"] = "object",
        ["String"] = "string",
        ["IntPtr"] = "nint",
        ["UIntPtr"] = "nuint",
    };

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

        /// <summary>
        ///     Did this member keep a real name from the original source?
        /// </summary>
        /// <param name="name">Member name</param>
        /// <param name="memberType">Field type or property return type</param>
        public bool IsReal(TypeSignature? memberType)
        {
            return !string.IsNullOrEmpty(str) && !str.IsObfuscatedName() && !str.IsTypeDerived(memberType);
        }

        private bool IsTypeDerived(TypeSignature? type)
        {
            if (type is null)
            {
                return false;
            }

            // strip the trailing counter the deobfuscator appends, eg bool_0 -> bool
            var baseName = str.TrimStart('_');
            var underscore = baseName.LastIndexOf('_');

            if (underscore <= 0 || !baseName[(underscore + 1)..].All(char.IsDigit))
            {
                return false;
            }

            baseName = baseName[..underscore];
            var typeName = type.Name?.ToString();

            if (baseName.Length == 0 || string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            // drop generics
            var tick = typeName.IndexOf('`');
            if (tick > 0)
            {
                typeName = typeName[..tick];
            }

            typeName = new string(typeName.Where(char.IsLetterOrDigit).ToArray());

            if (typeName.Length == 0)
            {
                return false;
            }

            return baseName.Equals(typeName, StringComparison.OrdinalIgnoreCase)
                || (
                    _keywordAliases.TryGetValue(typeName, out var alias)
                    && baseName.Equals(alias, StringComparison.OrdinalIgnoreCase)
                );
        }
    }

}
