using AsmResolver.DotNet;
using AssemblyLib.DirectMapper.Patches;
using AssemblyLib.DirectMapper.Renamers;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable(InjectionType.Singleton)]
public class DirectMapController(
    AttributeFactory attributeFactory,
    AssemblyWriter assemblyWriter,
    DataProvider dataProvider,
    RenamerService renamerService,
    SigBasedMemberRenamer sigBasedMemberRenamer,
    Publicizer publicizer,
    MemberReferenceCache memberReferenceCache,
    IEnumerable<IPatch> patches
)
{
    private ModuleDefinition? Module { get; set; }
    private List<TypeDefinition> Types { get; set; } = [];

    private string _targetAssemblyPath = string.Empty;

    public async Task Run(string assemblyPath, string? dummyDllPath)
    {
        Module = dataProvider.LoadModule(assemblyPath);
        _targetAssemblyPath = assemblyPath;

        if (!TryDeobfuscateAssembly())
        {
            return;
        }

        await memberReferenceCache.Hydrate();
        await RunRenamingProcess();

        if (!string.IsNullOrEmpty(dummyDllPath))
        {
            dataProvider.LoadDummyDllModule(dummyDllPath);
            Log.Information("Dummy DLL loaded.");
            await RenameBySignature();
        }

        await PublicizeObfuscatedTypes();

        // We need the publication to be complete before renaming fields
        // due to the differences in conventions between public and private
        renamerService.PostPublicizeRenameStage();

        await UpdateAttributes();
        await ApplyPatches();
        await assemblyWriter.WriteAssembly(Module, _targetAssemblyPath);

        Log.Information("Direct map completed.");
    }

    private bool TryDeobfuscateAssembly()
    {
        var result = assemblyWriter.Deobfuscate(Module, _targetAssemblyPath);
        if (!result.Success)
        {
            return false;
        }

        // ReSharper disable once JoinNullCheckWithUsage
        if (result.DeObfuscatedAssemblyPath is null)
        {
            throw new NullReferenceException("Deobfuscated assembly path is null");
        }

        // ReSharper disable once JoinNullCheckWithUsage - changing this fixed the deobfuscation bug
        if (result.DeObfuscatedModule is null)
        {
            throw new NullReferenceException("Deobfuscated module is null");
        }

        _targetAssemblyPath = result.DeObfuscatedAssemblyPath;
        Module = result.DeObfuscatedModule;

        Types.AddRange(Module?.GetAllTypes() ?? []);

        if (Types.Count == 0)
        {
            throw new InvalidOperationException("No types found during loading/deobfuscation of assembly");
        }

        return true;
    }

    private async Task RunRenamingProcess()
    {
        var mappings = dataProvider.DirectMapModels;

        if (mappings.Count == 0)
        {
            Log.Error("No direct-mappings loaded.");
            return;
        }

        foreach (var (targetFullName, mapping) in mappings)
        {
            await renamerService.RenameMappingRecursive(targetFullName, mapping);
        }

        attributeFactory.UpdateAsyncAttributes();

        // Make sure we don't do this until after renaming remaps
        renamerService.RenameCompilerGeneratedTypes();
    }

    private Task ApplyPatches()
    {
        foreach (var patch in patches)
        {
            patch.Patch();
        }

        return Task.CompletedTask;
    }

    private Task RenameBySignature()
    {
        sigBasedMemberRenamer.RenameMembersBySignature();
        return Task.CompletedTask;
    }

    private async Task PublicizeObfuscatedTypes()
    {
        foreach (var type in Types)
        {
            await publicizer.PublicizeType(type);
        }
    }

    private Task UpdateAttributes()
    {
        var mappingDict = new Dictionary<string, TypeDefinition>();
        foreach (var (fullName, mapping) in dataProvider.DirectMapModels)
        {
            mappingDict.Add(fullName, mapping.ToolData.Type!);
        }

        attributeFactory.UpdateAllJsonConverterAttributes(mappingDict);
        attributeFactory.UpdateAllTypeConverterAttributes(mappingDict);

        return Task.CompletedTask;
    }
}
