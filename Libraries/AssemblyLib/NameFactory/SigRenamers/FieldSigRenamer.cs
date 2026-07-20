using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.Helpers;
using AssemblyLib.SignatureComparers;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory.SigRenamers;

/// <summary>
///     Names obfuscated fields from the dummy assembly
/// </summary>
[Injectable]
public class FieldSigRenamer(
    ILogger<FieldSigRenamer> logger,
    FieldSigComparer fieldSigComparer,
    DirectRenameCache directRenameCache,
    MemberReferenceCache memberReferenceCache
) : ISigRenamer
{
    /// <summary>
    ///     Groups holding more than one field per side can only be paired by declaration order.
    ///     Larger groups than this are left alone, zero turns ordinal pairing off entirely.
    /// </summary>
    private const int MaxOrdinalChainLength = 3;

    // Runs after the property pass so backing fields see corrected property names
    public int Priority => -50;
    public bool Enabled => true;
    public ERenamerType Type => ERenamerType.Fields;

    public void Rename(TypeDefinition targetType, TypeDefinition dummyType)
    {
        var candidates = GetCandidates(targetType);

        if (candidates.Count == 0)
        {
            return;
        }

        var backingFields = BuildBackingFieldMap(targetType);
        var remaining = new List<FieldDefinition>();

        foreach (var field in candidates)
        {
            if (backingFields.TryGetValue(field, out var property))
            {
                if (!property.Name!.IsReal(property.Signature?.ReturnType))
                {
                    remaining.Add(field);
                    continue;
                }

                TryRename(field, GetBackingFieldName(property));
                continue;
            }

            remaining.Add(field);
        }

        if (remaining.Count == 0)
        {
            return;
        }

        var dummyPool = GetDummyPool(targetType, dummyType);

        // Derived names rather than guesses, so these run first and shrink the groups below
        MatchByKey(remaining, dummyPool, GetJsonPropertyName);
        MatchByKey(remaining, dummyPool, GetConstantKey);

        // Alignment needs anchors, so it runs between the passes that make them
        MatchByAlignment(targetType, dummyType, remaining, dummyPool);
        MatchBySignature(remaining, dummyPool, forcedOnly: true);
        MatchByAlignment(targetType, dummyType, remaining, dummyPool);

        // Ordinal guesses go last, they would poison the anchors above
        MatchBySignature(remaining, dummyPool, forcedOnly: false);
        MatchByAlignment(targetType, dummyType, remaining, dummyPool);
    }

    /// <param name="forcedOnly">Take only groups holding one field per side, where nothing is guessed</param>
    private void MatchBySignature(List<FieldDefinition> remaining, List<FieldDefinition> dummyPool, bool forcedOnly)
    {
        foreach (var group in PartitionBySignature(remaining))
        {
            var dummies = dummyPool.Where(d => fieldSigComparer.IsSame(group[0], d)).ToList();

            // Different counts mean the two sides have drifted, so pairing would be offset
            if (dummies.Count != group.Count)
            {
                continue;
            }

            // A single field each side is forced, longer runs lean on declaration order
            if (group.Count > 1 && (forcedOnly || group.Count > MaxOrdinalChainLength))
            {
                continue;
            }

            for (var i = 0; i < group.Count; i++)
            {
                if (!TryRename(group[i], dummies[i].Name?.ToString()))
                {
                    continue;
                }

                remaining.Remove(group[i]);
                dummyPool.Remove(dummies[i]);
            }
        }
    }

    private List<FieldDefinition> GetCandidates(TypeDefinition targetType)
    {
        var candidates = new List<FieldDefinition>();

        foreach (var field in targetType.Fields)
        {
            if (directRenameCache.Contains(field))
            {
                continue;
            }

            // Renaming these breaks unity deserialization
            if (field.IsUnitySerializedField())
            {
                continue;
            }

            if (field.Name!.IsReal(field.Signature?.FieldType))
            {
                continue;
            }

            candidates.Add(field);
        }

        return candidates;
    }

    /// <summary>
    ///     Dummy fields allowed to donate a name, using the same exclusions as the target side
    /// </summary>
    private static List<FieldDefinition> GetDummyPool(TypeDefinition targetType, TypeDefinition dummyType)
    {
        var targetNames = targetType.Fields.Select(f => f.Name?.ToString()).ToHashSet();

        return dummyType
            .Fields.Where(d => !d.IsUnitySerializedField())
            // Backing fields come from the property side, and the dummy has no bodies to check
            .Where(d => !IsCompilerBackingFieldName(d.Name?.ToString()))
            .Where(d => !targetNames.Contains(d.Name?.ToString()))
            .ToList();
    }

    /// <summary>
    ///     Maps fields by position, but only once the two sides are shown to line up. Fields that
    ///     already carry a real name act as anchors, so an added, removed or reordered field puts an
    ///     anchor at the wrong index and the whole type is skipped.
    /// </summary>
    private void MatchByAlignment(
        TypeDefinition targetType,
        TypeDefinition dummyType,
        List<FieldDefinition> remaining,
        List<FieldDefinition> dummyPool
    )
    {
        var targetFields = targetType.Fields;
        var dummyFields = dummyType.Fields;

        if (targetFields.Count == 0 || targetFields.Count != dummyFields.Count)
        {
            return;
        }

        var anchors = 0;

        for (var i = 0; i < targetFields.Count; i++)
        {
            var targetName = targetFields[i].Name;
            var dummyName = dummyFields[i].Name?.ToString();

            if (targetName is null || dummyName is null)
            {
                return;
            }

            if (targetName.IsReal(targetFields[i].Signature?.FieldType))
            {
                if (targetName.ToString() != NormalizeName(dummyName))
                {
                    return;
                }

                anchors++;
                continue;
            }

            // An unnamed field still has to agree on type
            if (targetFields[i].Signature?.FieldType.Name != dummyFields[i].Signature?.FieldType.Name)
            {
                return;
            }
        }

        // With nothing already named there is no proof the two sides line up
        if (anchors == 0)
        {
            return;
        }


        for (var i = 0; i < targetFields.Count; i++)
        {
            var field = targetFields[i];

            if (!remaining.Contains(field) || !TryRename(field, NormalizeName(dummyFields[i].Name!.ToString())))
            {
                continue;
            }

            remaining.Remove(field);
            dummyPool.Remove(dummyFields[i]);
        }
    }

    /// <summary>
    ///     The dummy keeps compiler backing field names, we use the underscore form
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (!IsCompilerBackingFieldName(name))
        {
            return name;
        }

        var close = name.IndexOf('>');

        if (close <= 1)
        {
            return name;
        }

        var inner = name[1..close];

        return $"_{char.ToLowerInvariant(inner[0])}{inner[1..]}";
    }

    /// <summary>
    ///     Pairs fields sharing an identifying key, only where that key is unique on both sides
    /// </summary>
    private void MatchByKey(
        List<FieldDefinition> remaining,
        List<FieldDefinition> dummyPool,
        Func<FieldDefinition, string?> keyOf
    )
    {
        var targets = UniqueByKey(remaining, keyOf);

        if (targets.Count == 0)
        {
            return;
        }

        var dummies = UniqueByKey(dummyPool, keyOf);

        foreach (var (key, field) in targets)
        {
            if (!dummies.TryGetValue(key, out var dummy) || !TryRename(field, dummy.Name?.ToString()))
            {
                continue;
            }

            remaining.Remove(field);
            dummyPool.Remove(dummy);
        }
    }

    private static Dictionary<string, FieldDefinition> UniqueByKey(
        List<FieldDefinition> fields,
        Func<FieldDefinition, string?> keyOf
    )
    {
        return fields
            .Select(f => (Field: f, Key: keyOf(f)))
            .Where(x => x.Key is not null)
            .GroupBy(x => x.Key!)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().Field);
    }

    /// <summary>
    ///     The name a field serializes under
    /// </summary>
    private static string? GetJsonPropertyName(FieldDefinition field)
    {
        foreach (var attribute in field.CustomAttributes)
        {
            if (attribute.Constructor?.DeclaringType?.Name != "JsonPropertyAttribute")
            {
                continue;
            }

            var name = attribute.Signature?.FixedArguments.FirstOrDefault()?.Element switch
            {
                Utf8String utf8 => utf8.ToString(),
                string text => text,
                _ => null,
            };

            if (!string.IsNullOrEmpty(name))
            {
                return $"json:{name}";
            }
        }

        return null;
    }

    /// <summary>
    ///     A constant's type and value fingerprint the field
    /// </summary>
    private static string? GetConstantKey(FieldDefinition field)
    {
        var constant = field.Constant;
        var data = constant?.Value?.Data;

        // An empty value tells us nothing
        if (constant is null || data is null || data.Length == 0)
        {
            return null;
        }

        return $"const:{constant.Type}:{Convert.ToBase64String(data)}";
    }

    /// <summary>
    ///     Groups fields the comparer treats as identical, keeping declaration order within a group
    /// </summary>
    private List<List<FieldDefinition>> PartitionBySignature(List<FieldDefinition> fields)
    {
        var groups = new List<List<FieldDefinition>>();

        foreach (var field in fields)
        {
            var group = groups.FirstOrDefault(g => fieldSigComparer.IsSame(g[0], field));

            if (group is null)
            {
                groups.Add([field]);
                continue;
            }

            group.Add(field);
        }

        return groups;
    }

    /// <returns>True if the field was renamed</returns>
    private bool TryRename(FieldDefinition field, string? newName)
    {
        if (string.IsNullOrEmpty(newName) || field.Name == newName)
        {
            return false;
        }

        var declaringType = field.DeclaringType;

        if (declaringType is not null && IsNameTaken(declaringType, newName))
        {
            // Event backing fields share the event name, which the publicizer turns into CS0229
            var fallback = $"_{char.ToLowerInvariant(newName[0])}{newName[1..]}";

            if (IsNameTaken(declaringType, fallback))
            {
                logger.LogWarning(
                    "Both {name} and {fallback} are taken on {type}. Skipping.",
                    newName,
                    fallback,
                    declaringType.Name?.ToString()
                );

                return false;
            }

            newName = fallback;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Renaming field [{type}::{old}] to [{type}::{new}]",
                field.DeclaringType,
                field.Name?.ToString(),
                field.DeclaringType,
                newName
            );
        }

        var utf8Name = new Utf8String(newName);

        UpdateFieldReferences(field, utf8Name);
        field.Name = utf8Name;

        return true;
    }

    private void UpdateFieldReferences(FieldDefinition field, Utf8String newName)
    {
        List<MemberReference> references;

        try
        {
            references = memberReferenceCache.GetFieldReferences(field);
        }
        catch (KeyNotFoundException)
        {
            return;
        }

        foreach (var reference in references)
        {
            reference.Name = newName;
        }
    }

    /// <summary>
    ///     Maps auto property backing fields to their property. A field claimed by two properties is
    ///     dropped as ambiguous.
    /// </summary>
    private static Dictionary<FieldDefinition, PropertyDefinition> BuildBackingFieldMap(TypeDefinition type)
    {
        var map = new Dictionary<FieldDefinition, PropertyDefinition>();
        var ambiguous = new HashSet<FieldDefinition>();

        foreach (var property in type.Properties)
        {
            var field = GetAutoPropertyBackingField(property);

            if (field is null || field.DeclaringType != type)
            {
                continue;
            }

            if (!map.TryAdd(field, property))
            {
                ambiguous.Add(field);
            }
        }

        foreach (var field in ambiguous)
        {
            map.Remove(field);
        }

        return map;
    }

    /// <summary>
    ///     The field an auto property getter reads, or null if it does anything more than return one
    /// </summary>
    private static FieldDefinition? GetAutoPropertyBackingField(PropertyDefinition property)
    {
        if (property.GetMethod?.CilMethodBody is null)
        {
            return null;
        }

        var body = property.GetMethod.CilMethodBody.Instructions.Where(i => i.OpCode != CilOpCodes.Nop).ToList();

        // instance: ldarg.0, ldfld <field>, ret
        if (
            body.Count == 3
            && body[0].OpCode == CilOpCodes.Ldarg_0
            && body[1].OpCode == CilOpCodes.Ldfld
            && body[2].OpCode == CilOpCodes.Ret
        )
        {
            return body[1].Operand as FieldDefinition;
        }

        // static: ldsfld <field>, ret
        if (body.Count == 2 && body[0].OpCode == CilOpCodes.Ldsfld && body[1].OpCode == CilOpCodes.Ret)
        {
            return body[0].Operand as FieldDefinition;
        }

        return null;
    }

    /// <summary>
    ///     Is any member already using this name? A field can't share one with a property, event or method
    /// </summary>
    private static bool IsNameTaken(TypeDefinition type, string name)
    {
        return type.Fields.Any(f => f.Name == name)
            || type.Events.Any(e => e.Name == name)
            || type.Properties.Any(p => p.Name == name)
            || type.Methods.Any(m => m.Name == name);
    }

    /// <summary>
    ///     Backing field name from its property, Succeed becomes _succeed
    /// </summary>
    private static string GetBackingFieldName(PropertyDefinition property)
    {
        var name = property.Name!.ToString();

        return $"_{char.ToLowerInvariant(name[0])}{name[1..]}";
    }

    private static bool IsCompilerBackingFieldName(string? name)
    {
        return name is not null && name.StartsWith('<') && name.EndsWith("k__BackingField", StringComparison.Ordinal);
    }
}
