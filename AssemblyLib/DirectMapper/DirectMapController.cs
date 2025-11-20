using System.Diagnostics;
using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Extensions;
using AssemblyLib.Models;
using AssemblyLib.Remapper;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable(InjectionType.Singleton)]
public class DirectMapController(AssemblyWriter assemblyWriter, DataProvider dataProvider)
{
    private ModuleDefinition? Module { get; set; }
    private List<TypeDefinition> Types { get; set; } = [];

    private string _targetAssemblyPath = string.Empty;

    public Task Run(string assemblyPath)
    {
        Module = dataProvider.LoadModule(assemblyPath);
        _targetAssemblyPath = assemblyPath;

        if (!TryDeobfuscateAssembly())
        {
            Log.Error("Failed to deobfuscate assembly, exiting.");
            return Task.CompletedTask;
        }

        RunRenamingProcess();
        WriteAssembly();

        return Task.CompletedTask;
    }

    private bool TryDeobfuscateAssembly()
    {
        var sw = Stopwatch.StartNew();

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

        Log.Information(
            "Deobfuscation completed. Took {time:F2} seconds. Deobfuscated assembly written to: {assemblyPath}",
            sw.ElapsedMilliseconds / 1000,
            result.DeObfuscatedAssemblyPath
        );

        return true;
    }

    private Task RunRenamingProcess()
    {
        var mappings = dataProvider.DirectMapModels;

        if (mappings.Count == 0)
        {
            Log.Error("No direct-mappings loaded.");
            return Task.CompletedTask;
        }

        foreach (var (targetFullName, mapping) in mappings)
        {
            HandleMappingRecursive(targetFullName, mapping);
        }

        return Task.CompletedTask;
    }

    private void HandleMappingRecursive(string targetFullName, DirectMapModel model, TypeDefinition? parent = null)
    {
        model.Type = parent ?? Types.FirstOrDefault(t => t.FullName == targetFullName);
        if (model.Type is null)
        {
            Log.Error("Failed to find type: {target}", targetFullName);
            return;
        }

        // Do children type's first so the parent can be used to find them
        if (model.NestedTypes is not null)
        {
            foreach (var (name, nestedModel) in model.NestedTypes)
            {
                var nestedType = model.Type.NestedTypes.FirstOrDefault(t => t.Name == name);
                if (nestedType is null)
                {
                    Log.Error("Failed to find nested type: {name} on parent {parent}", name, model.Type.FullName);
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

        RenameMapping(model);
    }

    private void RenameMapping(DirectMapModel model)
    {
        model.Type?.Name = new Utf8String(model.NewName!);
        var oldName = model.Type?.FullName;

        if (model.NewNamespace is not null)
        {
            model.Type?.Namespace = new Utf8String(model.NewNamespace);
        }
        Log.Information("Type: {old} -> {new}", oldName, model.Type?.FullName);

        RenameMethods(model);
    }

    private static void RenameMethods(DirectMapModel model)
    {
        var methodsToRename = model.MethodRenames;
        if (methodsToRename is null || methodsToRename.Count == 0)
        {
            return;
        }

        foreach (var method in model.Type?.Methods ?? [])
        {
            if (method.Name is null || method.IsCompilerControlled || method.IsGetMethod || method.IsSetMethod)
            {
                continue;
            }

            if (methodsToRename.TryGetValue(method.Name, out var newName))
            {
                Log.Information("\t\tMethod: {old} -> {new}", method.Name.ToString(), newName);
                method.Name = new Utf8String(newName);
            }
        }
    }

    private Task WriteAssembly()
    {
        const string dllName = "-cleaned-direct-mapped-publicized.dll";
        var outPath = Path.Combine(
            Path.GetDirectoryName(_targetAssemblyPath)
                ?? throw new NullReferenceException("Target assembly path is null"),
            Module?.Name?.Replace(".dll", dllName) ?? Utf8String.Empty
        );

        try
        {
            Module!.Assembly!.Write(outPath);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Failed to write assembly to: {outPath}", outPath);
            throw;
        }

        Log.Information("Direct map completed. Assembly written to: {outPath}", outPath);

        return Task.CompletedTask;
    }
}
