using System.Collections.Concurrent;
using System.Diagnostics;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AssemblyLib.Extensions;
using AssemblyLib.Models;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable(InjectionType.Singleton)]
public class DirectMapController(
    AssemblyWriter assemblyWriter,
    DataProvider dataProvider,
    Renamer renamer,
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
            Log.Error("Failed to deobfuscate assembly, exiting.");
            return;
        }

        RunRenamingProcess();
        PublicizeObfuscatedTypes();
        await assemblyWriter.WriteAssembly(Module, _targetAssemblyPath);
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

        RenameMapping(model);
        publicizer.PublicizeType(model);

        renamer.RenameObfuscatedFields(Module!, toolData.ShortOldName!, model.NewName);
        renamer.RenameObfuscatedProperties(Module!, toolData.ShortOldName!, model.NewName);
    }

    private void RenameMapping(DirectMapModel model)
    {
        var toolData = model.ToolData;

        toolData.FullOldName = model.ToolData.Type?.FullName;
        toolData.ShortOldName = toolData.Type!.Name!.ToString();

        toolData.Type?.Name = new Utf8String(model.NewName!);

        if (model.NewNamespace is not null)
        {
            toolData.Type?.Namespace = new Utf8String(model.NewNamespace);
        }
        Log.Information("Type: {old} -> {new}", toolData.FullOldName, toolData.Type?.FullName);

        RenameMethods(model);

        publicizer.PublicizeType(model);
    }

    private void RenameMethods(DirectMapModel model)
    {
        var toolData = model.ToolData;

        if (toolData.Type?.IsInterface ?? false)
        {
            RenameInterfacePrefacedMethods(toolData.Type);
        }

        var methodsToRename = model.MethodRenames;
        if (methodsToRename is null || methodsToRename.Count == 0)
        {
            return;
        }

        foreach (var method in toolData.Type?.Methods ?? [])
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

    private void RenameInterfacePrefacedMethods(TypeDefinition interfaceToRenameFor)
    {
        var implementations = Module!.GetAllTypes().Where(t => t.Implements(interfaceToRenameFor.FullName));
        if (!implementations.Any())
        {
            return;
        }

        Log.Information("Fixing method names for interface: {interface}", interfaceToRenameFor.FullName);

        var interfaceMethodNames = interfaceToRenameFor.Methods.Select(t => t.Name!.ToString()).ToArray();

        foreach (var method in implementations.SelectMany(t => t.Methods))
        {
            var methodSplitName = method.Name!.Split('.');
            if (methodSplitName.Length <= 1)
            {
                continue;
            }

            var realMethodName = methodSplitName.Last();

            // Not a method impl from this interface
            if (!interfaceMethodNames.Contains(realMethodName))
            {
                continue;
            }

            method.Name = new Utf8String($"{interfaceToRenameFor.Name}.{realMethodName}");
        }
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
