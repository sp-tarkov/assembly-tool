using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Extensions;
using AssemblyLib.Models;
using AssemblyLib.Shared;
using Serilog;
using Spectre.Console;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Renamers;

[Injectable]
public class TypeRenamer(DataProvider dataProvider) : IRenamer
{
    public int Priority { get; } = 2;

    public ERenamerType Type
    {
        get { return ERenamerType.Type; }
    }

    private readonly Dictionary<string, int> _classCounters = [];
    private readonly Dictionary<string, int> _structCounters = [];

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

    public void RenameCompilerGeneratedTypes()
    {
        RenameCompilerGeneratedClasses();
        RenameCompilerGeneratedStructs();
    }

    /// <summary>
    ///     This renames all compiler generated classes, this should only run AFTER the mapping process
    /// </summary>
    private void RenameCompilerGeneratedClasses()
    {
        if (_classCounters.Count != 0)
        {
            Log.Error("Already renamed compiler generated classes.");
            return;
        }

        var compilerClasses = dataProvider.LoadedModule!.GetAllTypes().Where(t => t.IsCompilerGenerated() && t.IsClass);
        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            foreach (var type in compilerClasses)
            {
                type.Name = GetNewCgClassName(type);
            }

            return;
        }

        AnsiConsole
            .Progress()
            .AutoClear(true)
            .StartAsync(ctx =>
            {
                var task = ctx.AddTask("[green]Renaming CG Classes[/]", maxValue: compilerClasses.Count());

                foreach (var type in compilerClasses)
                {
                    type.Name = GetNewCgClassName(type);
                    task.Increment(1.0);
                }

                return Task.CompletedTask;
            });
    }

    /// <summary>
    ///     This renames all compiler generated structs, this should only run AFTER the mapping process
    /// </summary>
    private void RenameCompilerGeneratedStructs()
    {
        if (_structCounters.Count != 0)
        {
            Log.Error("Already renamed compiler generated structs.");
            return;
        }

        var compilerStructs = dataProvider
            .LoadedModule!.GetAllTypes()
            .Where(t => t.IsCompilerGenerated() && t.InheritsFrom("System.ValueType") && !t.IsEnum);

        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            foreach (var type in compilerStructs)
            {
                type.Name = GetNewCgStructName(type);
            }

            return;
        }

        AnsiConsole
            .Progress()
            .AutoClear(true)
            .StartAsync(ctx =>
            {
                var task = ctx.AddTask("[green]Renaming CG Structs[/]", maxValue: compilerStructs.Count());

                foreach (var type in compilerStructs)
                {
                    type.Name = GetNewCgStructName(type);
                    Log.Information("Renamed: {struct}", type.Name.ToString());
                    task.Increment(1.0);
                }

                return Task.CompletedTask;
            });
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
                return new Utf8String("CGClass");
            }

            // Increment the count return the name
            _classCounters["ROOT"]++;
            return new Utf8String($"CGClass{count}");
        }

        var name = type.DeclaringType?.Name?.ToString();
        if (!_classCounters.TryGetValue(declaringType.FullName, out var localCount))
        {
            // This is our first class in the namespace
            _classCounters[declaringType.FullName] = localCount = 0;
            return new Utf8String($"CGClass{localCount}");
        }

        // Increment the count return the name
        _classCounters[declaringType.FullName]++;
        return new Utf8String($"CGClass{localCount}");
    }

    /// <summary>
    ///     Generates a new compiler generated class name for a given type
    /// </summary>
    /// <param name="type">Type to generate the name for</param>
    /// <returns>New name</returns>
    private Utf8String GetNewCgStructName(TypeDefinition type)
    {
        var declaringType = type.DeclaringType;
        if (declaringType is null)
        {
            if (!_structCounters.TryGetValue("ROOT", out var count))
            {
                // This is our first in global scope
                _structCounters["ROOT"] = 0;
                return new Utf8String("CGStruct");
            }

            // Increment the count return the name
            _structCounters["ROOT"]++;
            return new Utf8String($"CGStruct{count}");
        }

        if (!_structCounters.TryGetValue(declaringType.FullName, out var localCount))
        {
            // This is our first class in the namespace
            _structCounters[declaringType.FullName] = localCount = 0;
            return new Utf8String($"CGStruct{localCount}");
        }

        // Increment the count return the name
        _structCounters[declaringType.FullName]++;
        return new Utf8String($"CGStruct{localCount}");
    }
}
