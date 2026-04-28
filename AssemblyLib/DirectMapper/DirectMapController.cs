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

    public async Task Run(string assemblyPath, string dummyDllPath)
    {
        if (!await PrepareStage(assemblyPath, dummyDllPath))
        {
            return;
        }

        await DirectMapStage();
        PostDirectMapStage();

        await assemblyWriter.WriteAssembly(Module!, _targetAssemblyPath);

        Log.Information("Direct map completed.");
    }

    /// <summary>
    ///     Handles preparing the tool for use including; loading the dlls, deobfuscating and hydrating the cache
    /// </summary>
    /// <param name="assemblyPath">Path to the target assembly</param>
    /// <param name="dummyDllPath">Path to the dummy dll from 1.0</param>
    /// <returns>true if tool is ready for use</returns>
    private async Task<bool> PrepareStage(string assemblyPath, string dummyDllPath)
    {
        Log.Information("Prepare data stage");

        Module = dataProvider.LoadModule(assemblyPath);
        _targetAssemblyPath = assemblyPath;

        if (!TryDeobfuscateAssembly())
        {
            return false;
        }

        dataProvider.LoadDummyDllModule(dummyDllPath);

        attributeFactory.PreInitializeAllAttributeSignatures();

        await memberReferenceCache.Hydrate();
        return true;
    }

    /// <summary>
    ///     Handles all actions relating directly to the direct mapping process including; direct mappings, signature mappings, and publication
    /// </summary>
    private async Task DirectMapStage()
    {
        Log.Information("Direct map stage");

        await RunDirectMappingProcess();
        sigBasedMemberRenamer.RenameMembersBySignature();
        RunPublicizer();
    }

    /// <summary>
    ///     Handles all actions after completing the direct mapping process including; Renaming obfuscated fields by on type name,
    /// fixing capitalization post publication, updating attributes and applying patches.
    /// </summary>
    private void PostDirectMapStage()
    {
        Log.Information("Post direct map stage");

        //renamerService.PostDirectMapStage();
        UpdateAttributes();
        ApplyPatches();
    }

    /// <summary>
    ///     Deobfuscates the assembly
    /// </summary>
    /// <returns>true if deobfuscation was successful</returns>
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

    /// <summary>
    ///     Runs the direct mapping process
    /// </summary>
    private async Task RunDirectMappingProcess()
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

    /// <summary>
    ///     Applies any existing patches
    /// </summary>
    /// <returns></returns>
    private void ApplyPatches()
    {
        foreach (var patch in patches.Where(p => p.Enabled))
        {
            patch.Patch();
        }
    }

    /// <summary>
    ///     Runs the publicizer over all types
    /// </summary>
    private void RunPublicizer()
    {
        foreach (var type in Types)
        {
            publicizer.PublicizeType(type);
        }
    }

    /// <summary>
    ///     Updates all attribute that need replaced
    /// </summary>
    private void UpdateAttributes()
    {
        var mappingDict = new Dictionary<string, TypeDefinition>();
        foreach (var (fullName, mapping) in dataProvider.DirectMapModels)
        {
            mappingDict.Add(fullName, mapping.ToolData.Type!);
        }

        attributeFactory.UpdateAllJsonConverterAttributes(mappingDict);
        attributeFactory.UpdateAllTypeConverterAttributes(mappingDict);
    }
}
