using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory.DirectMapRenamers;

[Injectable]
public class TypeDirectMapRenamer(ILogger<TypeDirectMapRenamer> logger, DataProvider dataProvider) : IDirectMapRenamer
{
    public int Priority => -1;
    public bool Enabled => true;

    public ERenamerType Type => ERenamerType.Type;

    private readonly Dictionary<string, int> _classCounters = [];
    private readonly Dictionary<string, int> _structCounters = [];

    public void Rename(DirectMapModel model)
    {
        var toolData = model.ToolData;

        if (!string.IsNullOrEmpty(model.NewNamespace))
        {
            toolData.Type?.Namespace = new Utf8String(model.NewNamespace);
        }

        // Not setting a new name
        if (model.NewName is null)
        {
            return;
        }

        var genericParametersCount = toolData.Type!.GenericParameters.Count;

        var utf8Name =
            genericParametersCount > 0
                ? new Utf8String($"{model.NewName!}`{genericParametersCount}")
                : new Utf8String(model.NewName!);

        toolData.Type?.Name = utf8Name;
    }

    public void RenameCompilerGeneratedTypes()
    {
        RenameCompilerGeneratedClasses();
        RenameCompilerGeneratedStructs();
    }

    /// <summary>
    ///     This renames all compiler generated classes, this should only run AFTER the mapping process.
    ///     Numbering is unique within each declaring type *and* across its base-class chain — a CG class
    ///     emitted into a derived type never reuses a number already used by a CG class on any base,
    ///     which would otherwise hide the inherited nested type (CS0108).
    /// </summary>
    private void RenameCompilerGeneratedClasses()
    {
        if (_classCounters.Count != 0)
        {
            logger.LogError("Already renamed compiler generated classes.");
            return;
        }

        // Order base-before-derived so that, by the time we reach a derived type's CG nested types,
        // its base's counter is already populated and we can seed off the high-water mark.
        var compilerClasses = dataProvider
            .LoadedModule!.GetAllTypes()
            .Where(t => t.IsCompilerGenerated() && t.IsClass)
            .OrderBy(t => GetInheritanceDepth(t.DeclaringType))
            .ToList();

        foreach (var type in compilerClasses)
        {
            type.Name = GetNewCgClassName(type);
        }
    }

    /// <summary>
    ///     This renames all compiler generated structs, this should only run AFTER the mapping process.
    ///     Same scoping rule as <see cref="RenameCompilerGeneratedClasses"/>.
    /// </summary>
    private void RenameCompilerGeneratedStructs()
    {
        if (_structCounters.Count != 0)
        {
            logger.LogError("Already renamed compiler generated structs.");
            return;
        }

        var compilerStructs = dataProvider
            .LoadedModule!.GetAllTypes()
            .Where(t => t.IsCompilerGenerated() && t.IsValueType && !t.IsEnum)
            .OrderBy(t => GetInheritanceDepth(t.DeclaringType))
            .ToList();

        foreach (var type in compilerStructs)
        {
            type.Name = GetNewCgStructName(type);
        }
    }

    /// <summary>
    ///     Generates a new compiler generated class name for a given type.
    /// </summary>
    private Utf8String GetNewCgClassName(TypeDefinition type) =>
        new($"CG_Class{NextNumber(type.DeclaringType, _classCounters)}");

    /// <summary>
    ///     Generates a new compiler generated struct name for a given type.
    /// </summary>
    private Utf8String GetNewCgStructName(TypeDefinition type) =>
        new($"CG_Struct{NextNumber(type.DeclaringType, _structCounters)}");

    /// <summary>
    ///     Allocates the next free number for a CG type whose declaring scope is
    ///     <paramref name="declaringType"/>. The counter for a scope is seeded from the highest
    ///     number already consumed by any of its base classes, so derived scopes start above the
    ///     base's high-water mark and never collide with an inherited nested type.
    /// </summary>
    /// <returns>Allocated number (always non-negative).</returns>
    private int NextNumber(TypeDefinition? declaringType, Dictionary<string, int> counters)
    {
        var key = declaringType?.FullName ?? "ROOT";

        if (!counters.ContainsKey(key))
        {
            // First time we're naming into this scope. Seed from the base chain so the
            // first allocation here is one past the highest number consumed in any base.
            counters[key] = GetMaxNumberInBaseChain(declaringType, counters);
        }

        return ++counters[key];
    }

    /// <summary>
    ///     Returns the highest counter value already recorded for any base class of
    ///     <paramref name="declaringType"/>, or -1 if no base has been seen yet (so that the
    ///     caller's <c>++counter</c> produces 0 for the first allocation).
    /// </summary>
    private int GetMaxNumberInBaseChain(TypeDefinition? declaringType, Dictionary<string, int> counters)
    {
        if (declaringType is null)
        {
            return -1;
        }

        var max = -1;
        var current = declaringType.BaseType;
        var guard = 0;

        while (current is not null && guard++ < 64)
        {
            if (!current.TryResolve(dataProvider.Context, out var baseDef))
            {
                break;
            }

            if (counters.TryGetValue(baseDef.FullName, out var c) && c > max)
            {
                max = c;
            }

            current = baseDef.BaseType;
        }

        return max;
    }

    /// <summary>
    ///     Counts how deep the inheritance chain of <paramref name="declaringType"/> goes.
    ///     Used purely as a stable ordering key so processing visits bases before derived.
    ///     A null declaring type (top-level CG types) sorts to depth 0.
    /// </summary>
    private int GetInheritanceDepth(TypeDefinition? declaringType)
    {
        if (declaringType is null)
        {
            return 0;
        }

        var depth = 0;
        var current = declaringType.BaseType;
        var guard = 0;

        while (current is not null && guard++ < 64)
        {
            if (!current.TryResolve(dataProvider.Context, out var baseDef))
            {
                break;
            }

            depth++;
            current = baseDef.BaseType;
        }

        return depth;
    }
}
