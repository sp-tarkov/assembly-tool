using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable(InjectionType.Singleton)]
public class DirectMapController(
    AssemblyWriter assemblyWriter,
    DataProvider dataProvider,
    RenamerService renamerService,
    Publicizer publicizer
)
{
    private ModuleDefinition? Module { get; set; }
    private List<TypeDefinition> Types { get; set; } = [];

    private string _targetAssemblyPath = string.Empty;

    public async Task Run(string assemblyPath)
    {
        Module = dataProvider.LoadModule(assemblyPath);
        _targetAssemblyPath = assemblyPath;

        if (!TryDeobfuscateAssembly())
        {
            return;
        }

        RunRenamingProcess();
        PublicizeObfuscatedTypes();
        await assemblyWriter.WriteAssembly(Module, _targetAssemblyPath);
    }

    private bool TryDeobfuscateAssembly()
    {
        var result = assemblyWriter.Deobfuscate(Module, _targetAssemblyPath);
        if (!result.Success)
        {
            return false;
        }

        _targetAssemblyPath =
            result.DeObfuscatedAssemblyPath ?? throw new NullReferenceException("Deobfuscated assembly path is null");
        Module = result.DeObfuscatedModule ?? throw new NullReferenceException("Deobfuscated module is null");

        Types.AddRange(Module?.GetAllTypes() ?? []);

        if (Types.Count == 0)
        {
            throw new InvalidOperationException("No types found during loading/deobfuscation of assembly");
        }

        return true;
    }

    private void RunRenamingProcess()
    {
        var mappings = dataProvider.DirectMapModels;

        if (mappings.Count == 0)
        {
            Log.Error("No direct-mappings loaded.");
            return;
        }

        foreach (var (targetFullName, mapping) in mappings)
        {
            HandleMappingRecursive(targetFullName, mapping);
        }
    }

    private void HandleMappingRecursive(string targetFullName, DirectMapModel model, TypeDefinition? parent = null)
    {
        var toolData = model.ToolData;

        toolData.Type = parent ?? Types.FirstOrDefault(t => t.FullName == targetFullName);
        if (toolData.Type is null)
        {
            Log.Error("Failed to find type: {target}", targetFullName);
            return;
        }

        // Do children type's first so the parent can be used to find them
        if (model.NestedTypes is not null)
        {
            foreach (var (name, nestedModel) in model.NestedTypes)
            {
                var nestedType = toolData.Type.NestedTypes.FirstOrDefault(t => t.Name == name);
                if (nestedType is null)
                {
                    var children = string.Join(", ", nestedType?.NestedTypes.Select(t => t.Name?.ToString()) ?? []);

                    Log.Error("Failed to find nested type: {name} on parent {parent}", name, toolData.Type.FullName);
                    Log.Error("Available children for {parent}: {children}", toolData.Type.FullName, children);
                    continue;
                }

                HandleMappingRecursive(name, nestedModel, nestedType);
            }
        }

        // We're purely an entry for nested types. Do nothing else.
        if (model.NewName is null)
        {
            return;
        }

        renamerService.RenameMapping(model);
    }

    private void PublicizeObfuscatedTypes()
    {
        Log.Information("Publicizing assembly please wait...");

        Parallel.ForEach(
            Types,
            type =>
            {
                publicizer.PublicizeType(type);
            }
        );
    }
}
