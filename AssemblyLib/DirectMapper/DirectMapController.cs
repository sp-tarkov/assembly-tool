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
public class DirectMapController(AssemblyWriter assemblyWriter, DataProvider dataProvider)
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
        await WriteAssembly();
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
                    var children = string.Join(", ", nestedType?.NestedTypes.Select(t => t.Name?.ToString()) ?? []);

                    Log.Error("Failed to find nested type: {name} on parent {parent}", name, model.Type.FullName);
                    Log.Error("Available children for {parent}: {children}", model.Type.FullName, children);
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
        model.OldName = model.Type?.FullName;
        model.Type?.Name = new Utf8String(model.NewName!);

        if (model.NewNamespace is not null)
        {
            model.Type?.Namespace = new Utf8String(model.NewNamespace);
        }
        Log.Information("Type: {old} -> {new}", model.OldName, model.Type?.FullName);

        RenameMethods(model);
    }

    private void RenameMethods(DirectMapModel model)
    {
        if (model.Type?.IsInterface ?? false)
        {
            RenameInterfacePrefacedMethods(model.Type);
        }

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

    private async Task WriteAssembly()
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

        await StartHollow();

        var hollowedDir = Path.GetDirectoryName(outPath);
        var hollowedPath = Path.Combine(hollowedDir!, "Assembly-CSharp-hollowed.dll");

        try
        {
            Module?.Write(hollowedPath);
        }
        catch (Exception e)
        {
            Log.Error("Exception during write hollow task:\n{Exception}", e.Message);
            return;
        }

        assemblyWriter.StartHDiffz(outPath);
    }

    /// <summary>
    /// Hollows out all logic from the dll
    /// </summary>
    private async Task StartHollow()
    {
        Log.Information("Creating Hollow...");

        var tasks = new List<Task>(Types.Count);

        foreach (var type in Types)
        {
            tasks.Add(
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        HollowType(type);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception in task:\n{ExMessage}", ex.Message);
                    }
                })
            );
        }

        await Task.WhenAll(tasks.ToArray());
    }

    private static void HollowType(TypeDefinition type)
    {
        foreach (var method in type.Methods.Where(m => m.HasMethodBody))
        {
            // Create a new empty CIL body
            var newBody = new CilMethodBody(method);

            // If the method returns something, return default value
            if (method.Signature?.ReturnType != null && method.Signature.ReturnType.ElementType != ElementType.Void)
            {
                // Push default value onto the stack
                newBody.Instructions.Add(CilOpCodes.Ldnull);
            }

            // Just return (for void methods)
            newBody.Instructions.Add(CilOpCodes.Ret);

            // Assign the new method body
            method.CilMethodBody = newBody;
        }
    }
}
