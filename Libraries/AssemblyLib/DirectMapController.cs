using AsmResolver.DotNet;
using AssemblyLib.AttributeFactory;
using AssemblyLib.Helpers;
using AssemblyLib.Models;
using AssemblyLib.NameFactory;
using AssemblyLib.Patching;
using AssemblyLib.Validation;
using SPTarkov.DI.Annotations;

namespace AssemblyLib;

[Injectable(InjectionType.Singleton)]
public class DirectMapController(
    ILogger<DirectMapController> logger,
    AttributeFactoryService attributeFactory,
    AssemblyWriter assemblyWriter,
    DataProvider dataProvider,
    RenamerService renamerService,
    Publicizer publicizer,
    MemberReferenceCache memberReferenceCache,
    MethodBodyNuker methodBodyNuker,
    PatchService patchService,
    AssemblyValidatorService validatorService,
    AssemblySelfReferenceHelper assemblySelfReferenceHelper
)
{
    private ModuleDefinition? Module { get; set; }
    private List<TypeDefinition> Types { get; set; } = [];

    private string _targetAssemblyPath = string.Empty;

    public void Run(string assemblyPath, string dummyDllPath)
    {
        try
        {
            if (!PrepareStage(assemblyPath, dummyDllPath))
            {
                return;
            }

            if (!validatorService.Validate(ValidationStage.PreMapping))
                return;

            DirectMapStage();
            PostDirectMapStage();

            if (!validatorService.Validate(ValidationStage.PostMapping))
            {
                return;
            }

            assemblyWriter.WriteAssembly(Module!, _targetAssemblyPath);

            logger.LogInformation("Direct map completed.");
        }
        catch (Exception e)
        {
            logger.LogCritical("Exception while running the direct map process: {e}", e.Message);
            throw;
        }
    }

    /// <summary>
    ///     Handles preparing the tool for use including; loading the dlls, deobfuscating and hydrating the cache
    /// </summary>
    /// <param name="assemblyPath">Path to the target assembly</param>
    /// <param name="dummyDllPath">Path to the dummy dll from 1.0</param>
    /// <returns>true if tool is ready for use</returns>
    private bool PrepareStage(string assemblyPath, string dummyDllPath)
    {
        try
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
        catch (Exception e)
        {
            logger.LogCritical("Exception while in prepare stage: {e}", e.Message);
            throw;
        }
    }

    /// <summary>
    ///     Handles all actions relating directly to the direct mapping process including; direct mappings, signature mappings, and publication
    /// </summary>
    private void DirectMapStage()
    {
        try
        {
            logger.LogInformation("Direct map stage");

            RunDirectMappingProcess();
            renamerService.RenameBySignature();

            RunPublicizer();
        }
        catch (Exception e)
        {
            logger.LogCritical("Exception while in direct map stage: {e}", e.Message);
            throw;
        }
    }

    /// <summary>
    ///     Handles all actions after completing the direct mapping process including; Renaming obfuscated fields by on type name,
    /// fixing capitalization post publication, updating attributes and applying patches.
    /// </summary>
    private void PostDirectMapStage()
    {
        try
        {
            logger.LogInformation("Post direct map stage");

            attributeFactory.UpdateConverterAttributes();

            FindAndRemoveTypesFromAssembly();
            assemblySelfReferenceHelper.RemoveSelfAssemblyReferences(dataProvider.LoadedModule!);

            assemblyWriter.WriteAssembly(
                Module!,
                _targetAssemblyPath,
                "-cleaned-direct-mapped-publicized-unpatched.dll"
            );

            patchService.ApplyPatches();
            assemblySelfReferenceHelper.RemoveSelfAssemblyReferences(dataProvider.LoadedModule!);
        }
        catch (Exception e)
        {
            logger.LogCritical("Exception while in post direct map stage: {e}", e.Message);
            throw;
        }
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
    private void RunDirectMappingProcess()
    {
        var mappings = dataProvider.DirectMapModels;

        if (mappings.Count == 0)
        {
            logger.LogError("No direct-mappings loaded.");
            return;
        }

        logger.LogInformation("Renaming direct mappings");
        foreach (var (targetFullName, mapping) in mappings)
        {
            renamerService.RenameMappingRecursive(targetFullName, mapping);
        }

        attributeFactory.UpdateAsyncAttributes();
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
