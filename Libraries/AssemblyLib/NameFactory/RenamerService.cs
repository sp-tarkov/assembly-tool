using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.NameFactory.DirectMapRenamers;
using AssemblyLib.NameFactory.SigRenamers;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory;

[Injectable]
public class RenamerService(
    ILogger<RenamerService> logger,
    DataProvider dataProvider,
    IEnumerable<IDirectMapRenamer> directRenamers,
    IEnumerable<ISigRenamer> sigRenamers,
    ObfuscatedFieldRenamer obfuscatedFieldRenamer
)
{
    // Key - Target :: Val - Dummy
    private readonly Dictionary<TypeDefinition, TypeDefinition> _targetToDummyMap = [];

    /// <summary>
    ///     Recursively rename the mapping file and all nested types
    /// </summary>
    /// <param name="targetFullName">Target GCType to rename</param>
    /// <param name="model">model</param>
    /// <param name="parent">parent used in recursive call</param>
    public void RenameMappingRecursive(string targetFullName, DirectMapModel model, TypeDefinition? parent = null)
    {
        var toolData = model.ToolData;

        try
        {
            SetupToolData(targetFullName, model, parent);
        }
        catch (Exception ex)
        {
            logger.LogError("Error setting up tool data: {message}", ex.Message);
            return;
        }

        if (toolData.Type is null)
        {
            logger.LogError("Failed to find type: {target}", targetFullName);
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

                    logger.LogError(
                        "Failed to find nested type: {name} on parent {parent}",
                        name,
                        toolData.Type.FullName
                    );
                    logger.LogError("Available children for {parent}: {children}", toolData.Type.FullName, children);
                    continue;
                }

                RenameMappingRecursive(name, nestedModel, nestedType);
            }
        }

        RenameMapping(model);
    }

    public void RenameCompilerGeneratedTypes()
    {
        if (directRenamers.FirstOrDefault(r => r is TypeDirectMapRenamer) is not TypeDirectMapRenamer classRenamer)
        {
            logger.LogError("Failed to find ClassRenamer type");
            return;
        }

        classRenamer.RenameCompilerGeneratedTypes();
    }

    public void RenameBySignature()
    {
        if (!dataProvider.IsDummyDllLoaded)
        {
            return;
        }

        var targetTypes = dataProvider
            .LoadedModule!.GetAllTypes()
            .Where(t => !t.FullName.IsObfuscatedName() && !t.IsCompilerGenerated() && !t.IsEnum)
            .ToList();

        var dummyTargetTypes = GetTargetTypesInDummy(targetTypes);
        BuildTargetToDummyMap(targetTypes, dummyTargetTypes);
        RunSigBasedRenamers();
    }

    private void RenameMapping(DirectMapModel model)
    {
        foreach (var renamer in directRenamers.Where(r => r.Enabled).OrderByDescending(r => r.Priority))
        {
            renamer.Rename(model);
        }
    }

    private void SetupToolData(string targetFullName, DirectMapModel model, TypeDefinition? type = null)
    {
        var toolData = model.ToolData;

        toolData.Type =
            type ?? dataProvider.LoadedModule!.GetAllTypes().FirstOrDefault(t => t.FullName == targetFullName);

        if (toolData.Type is null)
        {
            throw new FailedToFindTypeException(
                $"Failed to find type: `{targetFullName}` in target assembly, names must be quantified by fullname including namespace or this is the wrong type."
            );
        }

        toolData.FullOldName = model.ToolData.Type?.FullName;
        toolData.ShortOldName = toolData.Type!.Name!.ToString();
    }

    private List<TypeDefinition> GetTargetTypesInDummy(IEnumerable<TypeDefinition> targetTypes)
    {
        var targetTypeNameList = targetTypes.Select(t => t.FullName).ToList();
        return dataProvider
            .DummyDllModule!.GetAllTypes()
            .Where(type => targetTypeNameList.Contains(type.FullName))
            .ToList();
    }

    private void BuildTargetToDummyMap(
        IEnumerable<TypeDefinition> targetTypes,
        IEnumerable<TypeDefinition> dummyTargetTypes
    )
    {
        foreach (var target in targetTypes)
        {
            var dummyType = dummyTargetTypes.FirstOrDefault(t => t.FullName == target.FullName);
            if (dummyType is null)
            {
                /*
                logger.LogWarning(
                    "Type: {typeName} does not exist in the dummy dll. Sig based renaming will not happen.",
                    target.FullName
                );
                */

                continue;
            }

            _targetToDummyMap.TryAdd(target, dummyType);
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Loaded {count} dummy types for member comparison", _targetToDummyMap.Count);
        }
    }

    private void RunSigBasedRenamers()
    {
        // First pass, handles actions that require both the target and the dummy
        foreach (var renamer in sigRenamers.Where(r => r.Enabled).OrderByDescending(r => r.Priority))
        {
            logger.LogInformation("Running {type} sig renamer", renamer.Type.ToString());

            foreach (var (targetType, dummyType) in _targetToDummyMap)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Renaming members on: {type}", targetType.FullName);
                }

                renamer.Rename(targetType, dummyType);
            }
        }

        logger.LogInformation("Fixing obfuscated members");

        // Second pass, handles actions that only require the target
        foreach (var type in dataProvider.LoadedModule!.GetAllTypes())
        {
            obfuscatedFieldRenamer.Rename(type);
            RenameExplicitInterfaceMethods(type);
        }
    }

    private void RenameExplicitInterfaceMethods(TypeDefinition typeDef)
    {
        foreach (var method in typeDef.Methods.Where(m => m.IsExplicitInterfaceImplementation()))
        {
            var splitName = method.Name?.Split('.');
            if (splitName is null || splitName.Length < 2)
            {
                continue;
            }

            var changedToken = false;
            for (var i = 0; i < splitName.Length; i++)
            {
                if (
                    splitName[i].IsObfuscatedName()
                    && dataProvider.DirectMapModels.TryGetValue(splitName[i], out var model)
                    && model.NewName != null
                )
                {
                    splitName[i] = model.NewName;
                    changedToken = true;
                }
            }

            if (changedToken)
            {
                var newName = string.Join(".", splitName);

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "Renaming explicit interface method {old} -> {new}",
                        method.Name?.ToString(),
                        newName
                    );
                }

                method.Name = new Utf8String(newName);
            }
        }
    }
}
