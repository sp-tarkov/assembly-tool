using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AssemblyLib.SignatureComparers;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory.SigRenamers;

[Injectable]
public class PropertySigRenamer(
    ILogger<PropertySigRenamer> logger,
    DataProvider dataProvider,
    PropertySigComparer propertySigComparer,
    DirectRenameCache directRenameCache
) : ISigRenamer
{
    public int Priority => 0;
    public bool Enabled => true;
    public ERenamerType Type => ERenamerType.Properties;

    public void Rename(TypeDefinition targetType, TypeDefinition dummyType)
    {
        var targetProperties = targetType.Properties;
        var dummyProperties = dummyType.Properties.ToList();

        // Removes properties that already exist
        dummyProperties.RemoveAll(f => targetProperties.Any(t => t.Name == f.Name));

        var dummyPropertiesNames = dummyProperties.Select(p => p.Name).ToHashSet();

        foreach (var targetProperty in targetProperties)
        {
            if (directRenameCache.Contains(targetProperty))
            {
                continue;
            }

            if (dummyPropertiesNames.Contains(targetProperty.Name))
            {
                continue;
            }

            // Do not rename already named properties as they are already most likely correct
            if (IsAlreadyNamed(targetProperty))
            {
                continue;
            }

            var isExplicitInterfaceProperty = IsExplicitInterfaceProperty(targetProperty);

            // The member portion of an explicit implementation is often already named even though
            // its interface type is still obfuscated (for example, GInterface214.ProfileId). The
            // explicit-interface pass fixes that type name from the MethodImpl declaration. Treating
            // this as an unnamed property here can pair it with any property of the same signature.
            if (isExplicitInterfaceProperty && HasNamedExplicitInterfaceMember(targetProperty))
            {
                continue;
            }

            var matches = dummyProperties
                .Where(p => IsExplicitInterfaceProperty(p) == isExplicitInterfaceProperty)
                .Where(p => propertySigComparer.IsSame(targetProperty, p))
                .ToList();

            // Signature alone cannot distinguish multiple explicit implementations with the same
            // property shape. It is safer to leave an ambiguous member unchanged than attach the
            // name of a different interface contract to its accessor body.
            if (isExplicitInterfaceProperty && matches.Count != 1)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "Skipping ambiguous explicit interface property {property}; "
                            + "found {count} matching dummy properties",
                        targetProperty.FullName,
                        matches.Count
                    );
                }

                continue;
            }

            var dummyProperty = matches.FirstOrDefault();
            if (dummyProperty is null)
            {
                continue;
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Renaming property: {old} -> {new}",
                    targetProperty.FullName,
                    dummyProperty.FullName
                );
            }

            targetProperty.Name = dummyProperty.Name;
            dummyProperties.Remove(dummyProperty);
        }
    }

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

    /// <summary>
    ///     Did this property already carry a real name in the original assembly?
    ///     False for obfuscated names and for names derived from the property's own type
    /// </summary>
    private static bool IsAlreadyNamed(PropertyDefinition property)
    {
        var name = property.Name?.ToString();

        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return !name.IsObfuscatedName() && !IsTypeDerivedName(name, property.Signature?.ReturnType);
    }

    private static bool IsTypeDerivedName(string name, TypeSignature? type)
    {
        if (type is null)
        {
            return false;
        }

        // strip the trailing counter the deobfuscator appends, eg bool_0 -> bool
        var baseName = name.TrimStart('_');
        var underscore = baseName.LastIndexOf('_');

        if (underscore > 0 && baseName[(underscore + 1)..].All(char.IsDigit))
        {
            baseName = baseName[..underscore];
        }

        if (baseName.Length == 0)
        {
            return false;
        }

        var typeName = type.Name?.ToString();

        if (string.IsNullOrEmpty(typeName))
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

    private static bool IsExplicitInterfaceProperty(PropertyDefinition property)
    {
        return property.GetMethod?.IsExplicitInterfaceImplementation() == true
            || property.SetMethod?.IsExplicitInterfaceImplementation() == true
            || property.Name?.ToString().Contains('.', StringComparison.Ordinal) == true;
    }

    private static bool HasNamedExplicitInterfaceMember(PropertyDefinition property)
    {
        var explicitTarget = property.GetMethod?.GetExplicitInterfaceTarget()
            ?? property.SetMethod?.GetExplicitInterfaceTarget();

        var targetMemberName = GetPropertyNameFromAccessor(explicitTarget?.Name?.ToString());
        if (targetMemberName is not null)
        {
            return !targetMemberName.IsObfuscatedName();
        }

        var propertyName = property.Name?.ToString();
        if (propertyName is null)
        {
            return false;
        }

        var separatorIndex = propertyName.LastIndexOf('.');
        if (separatorIndex < 0 || separatorIndex == propertyName.Length - 1)
        {
            return false;
        }

        return !propertyName[(separatorIndex + 1)..].IsObfuscatedName();
    }

    private static string? GetPropertyNameFromAccessor(string? accessorName)
    {
        if (accessorName is null)
        {
            return null;
        }

        if (accessorName.StartsWith("get_", StringComparison.Ordinal)
            || accessorName.StartsWith("set_", StringComparison.Ordinal))
        {
            return accessorName[4..];
        }

        return accessorName;
    }
}
