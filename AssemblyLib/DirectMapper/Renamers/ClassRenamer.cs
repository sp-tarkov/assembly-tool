using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Renamers;

[Injectable]
public class ClassRenamer(DataProvider dataProvider) : IRenamer
{
    public int Priority { get; } = 2;

    public ERenamerType Type
    {
        get { return ERenamerType.Class; }
    }

    private readonly Dictionary<string, int> _classCounters = [];

    public void Rename(DirectMapModel model)
    {
        var toolData = model.ToolData;

        toolData.FullOldName = model.ToolData.Type?.FullName;
        toolData.ShortOldName = toolData.Type!.Name!.ToString();

        if (!string.IsNullOrEmpty(model.NewNamespace))
        {
            toolData.Type?.Namespace = new Utf8String(model.NewNamespace);
        }

        var genericParametersCount = toolData.Type!.GenericParameters.Count;

        var utf8Name =
            genericParametersCount > 0
                ? new Utf8String($"{model.NewName!}`{genericParametersCount}")
                : new Utf8String(model.NewName!);

        toolData.Type?.Name = utf8Name;
    }

    /// <summary>
    ///     This renames all compiler generated classes, this should only run AFTER the mapping process
    /// </summary>
    public void RenameCompilerGeneratedClasses()
    {
        if (_classCounters.Count != 0)
        {
            Log.Error("Already renamed compiler generated types.");
            return;
        }

        var enumeratedTypes = dataProvider.LoadedModule!.GetAllTypes().Where(t => t.IsCompilerGenerated());
        Log.Information("Found {count} compiler generated types", enumeratedTypes.Count());

        foreach (var type in enumeratedTypes)
        {
            type.Name = GetNewCgClassName(type);
        }
    }

    /// <summary>
    ///     Generates a new compiler generated class name for a given type
    /// </summary>
    /// <param name="type">Type to generate the name for</param>
    /// <returns>New name</returns>
    private Utf8String GetNewCgClassName(TypeDefinition type)
    {
        var declaringType = type.DeclaringType;
        if (declaringType is null)
        {
            if (!_classCounters.TryGetValue("ROOT", out var count))
            {
                // This is our first in global scope
                _classCounters["ROOT"] = 0;
                return new Utf8String("CG_GlobalClass");
            }

            // Increment the count return the name
            _classCounters["ROOT"]++;
            return new Utf8String($"CG_GlobalClass{count}");
        }

        var name = type.DeclaringType?.Name?.ToString();
        if (!_classCounters.TryGetValue(declaringType.FullName, out var localCount))
        {
            // This is our first class in the namespace
            _classCounters[declaringType.FullName] = localCount = 0;
            return new Utf8String($"CG_{name}{localCount}");
        }

        // Increment the count return the name
        _classCounters[declaringType.FullName]++;
        return new Utf8String($"CG_{name}{localCount}");
    }
}
