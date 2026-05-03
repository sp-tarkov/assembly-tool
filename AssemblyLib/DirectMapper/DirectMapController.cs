using AsmResolver.DotNet;
using AssemblyLib.DirectMapper.AttributeFactory;
using AssemblyLib.DirectMapper.NameFactory;
using AssemblyLib.DirectMapper.Patches;
using AssemblyLib.Helpers;
using AssemblyLib.Models;
using AssemblyLib.Patching;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable(InjectionType.Singleton)]
public class DirectMapController(
    ILogger<DirectMapController> logger,
    AttributeFactoryService attributeFactory,
    AssemblyWriter assemblyWriter,
    DataProvider dataProvider,
    RenamerService renamerService,
    SigBasedMemberRenamer sigBasedMemberRenamer,
    Publicizer publicizer,
    MemberReferenceCache memberReferenceCache,
    MethodBodyNuker methodBodyNuker,
    IEnumerable<IPatch> patches
)
{
    private ModuleDefinition? Module { get; set; }
    private List<TypeDefinition> Types { get; set; } = [];

    private string _targetAssemblyPath = string.Empty;

    public async Task Run(string assemblyPath, string dummyDllPath)
    {
        if (!PrepareStage(assemblyPath, dummyDllPath))
        {
            return;
        }

        await DirectMapStage();
        PostDirectMapStage();

        await assemblyWriter.WriteAssembly(Module!, _targetAssemblyPath);

        logger.LogInformation("Direct map completed.");
    }

    /// <summary>
    ///     Handles preparing the tool for use including; loading the dlls, deobfuscating and hydrating the cache
    /// </summary>
    /// <param name="assemblyPath">Path to the target assembly</param>
    /// <param name="dummyDllPath">Path to the dummy dll from 1.0</param>
    /// <returns>true if tool is ready for use</returns>
    private bool PrepareStage(string assemblyPath, string dummyDllPath)
    {
        logger.LogInformation("Prepare data stage");

        Module = dataProvider.LoadModule(assemblyPath);
        _targetAssemblyPath = assemblyPath;

        if (!TryDeobfuscateAssembly())
        {
            return false;
        }

        dataProvider.LoadDummyDllModule(dummyDllPath);

        attributeFactory.PreInitializeAllAttributeSignatures();

        memberReferenceCache.Hydrate();
        return true;
    }

    /// <summary>
    ///     Handles all actions relating directly to the direct mapping process including; direct mappings, signature mappings, and publication
    /// </summary>
    private async Task DirectMapStage()
    {
        logger.LogInformation("Direct map stage");

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
        logger.LogInformation("Post direct map stage");

        //renamerService.PostDirectMapStage();
        attributeFactory.UpdateConverterAttributes();

        FindAndRemoveTypesFromAssembly();
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
            logger.LogError("No direct-mappings loaded.");
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
            logger.LogInformation("Patch {patchName} applied.", patch.GetType().Name);
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

    private void FindAndRemoveTypesFromAssembly()
    {
        foreach (var (_, model) in dataProvider.DirectMapModels)
        {
            ParseModelForTypesToRemove(model);
        }
    }

    private void ParseModelForTypesToRemove(DirectMapModel model)
    {
        if (model.NestedTypes is not null)
        {
            foreach (var (_, nestedModel) in model.NestedTypes)
            {
                ParseModelForTypesToRemove(nestedModel);
            }
        }

        if (!(model.RemoveType ?? false) || model.ToolData.Type is null)
        {
            return;
        }

        methodBodyNuker.NukeType(model.ToolData.Type);
    }
}
