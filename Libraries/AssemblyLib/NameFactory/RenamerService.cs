using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
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
            RenameExplicitInterfaceMembers(type);
        }
    }

    private void RenameExplicitInterfaceMembers(TypeDefinition typeDef)
    {
        foreach (var method in typeDef.Methods.Where(m => m.IsExplicitInterfaceImplementation()))
        {
            RenameExplicitInterfaceMethod(method, method.GetExplicitInterfaceTarget());
        }

        var propertyNameCounts = typeDef.Properties
            .GroupBy(GetPropertyCollisionKey)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var property in typeDef.Properties)
        {
            var explicitTarget = property.GetMethod?.GetExplicitInterfaceTarget()
                ?? property.SetMethod?.GetExplicitInterfaceTarget();

            RenameExplicitInterfaceProperty(property, explicitTarget, propertyNameCounts);
        }

        foreach (var @event in typeDef.Events)
        {
            var explicitTarget = @event.AddMethod?.GetExplicitInterfaceTarget()
                ?? @event.RemoveMethod?.GetExplicitInterfaceTarget()
                ?? @event.FireMethod?.GetExplicitInterfaceTarget();

            RenameExplicitInterfaceMember(@event, explicitTarget);
        }
    }

    private void RenameExplicitInterfaceMethod(MethodDefinition method, IMethodDefOrRef? explicitTarget)
    {
        var oldName = method.Name?.ToString();
        if (oldName is null)
        {
            return;
        }

        var newName = GetExplicitInterfaceMethodName(oldName, explicitTarget);
        if (newName is null || oldName == newName)
        {
            return;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Renaming explicit interface method {old} -> {new}", oldName, newName);
        }

        UpdateExplicitInterfaceDeclarationReferences(explicitTarget);
        method.Name = new Utf8String(newName);
    }

    private void RenameExplicitInterfaceProperty(
        PropertyDefinition property,
        IMethodDefOrRef? explicitTarget,
        Dictionary<(string Name, string Params), int> propertyNameCounts
    )
    {
        var oldName = property.Name?.ToString();
        if (oldName is null)
        {
            return;
        }

        var newName = GetExplicitInterfaceMemberName(oldName, explicitTarget);
        if (newName is null || oldName == newName)
        {
            return;
        }

        var oldKey = GetPropertyCollisionKey(property);
        var newKey = (newName, oldKey.Params);
        var existingNewNameCount = propertyNameCounts.GetValueOrDefault(newKey);
        var selfAlreadyHasNewName = oldKey == newKey ? 1 : 0;

        if (existingNewNameCount > selfAlreadyHasNewName)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Skipping explicit interface property rename {old} -> {new}; target name already exists",
                    oldName,
                    newName
                );
            }

            return;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Renaming explicit interface member {old} -> {new}", oldName, newName);
        }

        DecrementCount(propertyNameCounts, oldKey);
        UpdateExplicitInterfaceDeclarationReferences(explicitTarget);
        property.Name = new Utf8String(newName);
        IncrementCount(propertyNameCounts, newKey);
    }

    private void RenameExplicitInterfaceMember(IMemberDefinition member, IMethodDefOrRef? explicitTarget)
    {
        var oldName = member.Name?.ToString();
        if (oldName is null)
        {
            return;
        }

        var newName = GetExplicitInterfaceMemberName(oldName, explicitTarget);
        if (newName is null || oldName == newName)
        {
            return;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Renaming explicit interface member {old} -> {new}", oldName, newName);
        }

        UpdateExplicitInterfaceDeclarationReferences(explicitTarget);
        SetMemberName(member, new Utf8String(newName));
    }

    private static (string Name, string Params) GetPropertyCollisionKey(PropertyDefinition property) =>
        (
            property.Name?.ToString() ?? string.Empty,
            property.Signature is null
                ? string.Empty
                : string.Join(",", property.Signature.ParameterTypes.Select(p => p.FullName))
        );

    private static void DecrementCount<TKey>(Dictionary<TKey, int> counts, TKey key)
        where TKey : notnull
    {
        if (!counts.TryGetValue(key, out var count))
        {
            return;
        }

        if (count <= 1)
        {
            counts.Remove(key);
            return;
        }

        counts[key] = count - 1;
    }

    private static void IncrementCount<TKey>(Dictionary<TKey, int> counts, TKey key)
        where TKey : notnull
    {
        counts[key] = counts.GetValueOrDefault(key) + 1;
    }

    private string? GetExplicitInterfaceMemberName(string oldName, IMethodDefOrRef? explicitTarget)
    {
        if (!HasObfuscatedExplicitInterfaceToken(oldName))
        {
            return null;
        }

        if (explicitTarget?.DeclaringType is { } declaringType)
        {
            var memberName = GetExplicitInterfaceTargetMemberName(oldName, explicitTarget);
            if (memberName is not null)
            {
                return $"{GetExplicitInterfaceTypeName(declaringType)}.{memberName}";
            }
        }

        var splitName = oldName.Split('.');
        if (splitName.Length < 2)
        {
            return null;
        }

        var changedToken = false;
        for (var i = 0; i < splitName.Length; i++)
        {
            if (!splitName[i].IsObfuscatedName())
            {
                continue;
            }

            var mappedTypeName = GetMappedTypeName(splitName[i]);
            if (mappedTypeName is null)
            {
                continue;
            }

            splitName[i] = mappedTypeName;
            changedToken = true;
        }

        return changedToken ? string.Join(".", splitName) : null;
    }

    private string? GetExplicitInterfaceMethodName(string oldName, IMethodDefOrRef? explicitTarget)
    {
        if (!HasObfuscatedExplicitInterfaceToken(oldName))
        {
            return null;
        }

        if (explicitTarget?.DeclaringType is { } declaringType
            && explicitTarget.Name?.ToString() is { } targetName)
        {
            return $"{GetExplicitInterfaceTypeName(declaringType)}.{targetName}";
        }

        return GetMappedExplicitInterfaceName(oldName);
    }

    private static bool HasObfuscatedExplicitInterfaceToken(string name)
    {
        var lastSeparator = name.LastIndexOf('.');
        if (lastSeparator <= 0)
        {
            return false;
        }

        var interfaceName = name[..lastSeparator];
        var tokens = interfaceName.Split(['.', '<', '>', ',', ' ', '[', ']'], StringSplitOptions.RemoveEmptyEntries);

        return tokens.Any(token => token.IsObfuscatedName());
    }

    private static string? GetExplicitInterfaceTargetMemberName(string oldName, IMethodDefOrRef explicitTarget)
    {
        var targetName = explicitTarget.Name?.ToString();
        if (targetName is null)
        {
            return oldName.Split('.').LastOrDefault();
        }

        return targetName switch
        {
            var name when name.StartsWith("get_", StringComparison.Ordinal) => name[4..],
            var name when name.StartsWith("set_", StringComparison.Ordinal) => name[4..],
            var name when name.StartsWith("add_", StringComparison.Ordinal) => name[4..],
            var name when name.StartsWith("remove_", StringComparison.Ordinal) => name[7..],
            var name when name.StartsWith("raise_", StringComparison.Ordinal) => name[6..],
            _ => targetName,
        };
    }

    private string? GetMappedTypeName(string oldName)
    {
        if (!dataProvider.DirectMapModels.TryGetValue(oldName, out var model) || model.NewName is null)
        {
            return null;
        }

        return string.IsNullOrEmpty(model.NewNamespace) ? model.NewName : $"{model.NewNamespace}.{model.NewName}";
    }

    private void UpdateExplicitInterfaceDeclarationReferences(IMethodDefOrRef? explicitTarget)
    {
        if (explicitTarget is null)
        {
            return;
        }

        UpdateTypeReferenceNames(explicitTarget.DeclaringType);

        if (explicitTarget.Signature is { } signature)
        {
            UpdateMethodSignatureTypeReferenceNames(signature);
        }
    }

    private void UpdateTypeReferenceNames(ITypeDefOrRef? type)
    {
        switch (type)
        {
            case null:
                return;
            case TypeSpecification { Signature: { } signature }:
                UpdateTypeSignatureReferenceNames(signature);
                return;
            case TypeReference typeReference:
                UpdateTypeReferenceName(typeReference);
                if (typeReference.Scope is ITypeDefOrRef declaringType)
                {
                    UpdateTypeReferenceNames(declaringType);
                }
                return;
        }
    }

    private void UpdateTypeSignatureReferenceNames(TypeSignature? signature)
    {
        switch (signature)
        {
            case null:
                return;
            case GenericInstanceTypeSignature genericSig:
                UpdateTypeReferenceNames(genericSig.GenericType);
                foreach (var typeArgument in genericSig.TypeArguments)
                {
                    UpdateTypeSignatureReferenceNames(typeArgument);
                }
                return;
            case TypeDefOrRefSignature typeSig:
                UpdateTypeReferenceNames(typeSig.Type);
                return;
            case CustomModifierTypeSignature modifierSig:
                UpdateTypeReferenceNames(modifierSig.ModifierType);
                UpdateTypeSignatureReferenceNames(modifierSig.BaseType);
                return;
            case TypeSpecificationSignature specificationSig:
                UpdateTypeSignatureReferenceNames(specificationSig.BaseType);
                return;
            case FunctionPointerTypeSignature { Signature: { } functionSig }:
                UpdateMethodSignatureTypeReferenceNames(functionSig);
                return;
        }
    }

    private void UpdateMethodSignatureTypeReferenceNames(MethodSignatureBase? signature)
    {
        if (signature is null)
        {
            return;
        }

        UpdateTypeSignatureReferenceNames(signature.ReturnType);
        foreach (var parameterType in signature.ParameterTypes)
        {
            UpdateTypeSignatureReferenceNames(parameterType);
        }

        if (signature is not MethodSignature methodSignature)
        {
            return;
        }

        foreach (var sentinelParameterType in methodSignature.SentinelParameterTypes)
        {
            UpdateTypeSignatureReferenceNames(sentinelParameterType);
        }
    }

    private void UpdateTypeReferenceName(TypeReference typeReference)
    {
        var oldName = typeReference.Name?.ToString();
        if (oldName is null || !dataProvider.DirectMapModels.TryGetValue(oldName, out var model))
        {
            return;
        }

        if (model.NewName is not null)
        {
            typeReference.Name = new Utf8String(model.NewName);
        }

        if (!string.IsNullOrEmpty(model.NewNamespace))
        {
            typeReference.Namespace = new Utf8String(model.NewNamespace);
        }
    }

    private string GetExplicitInterfaceTypeName(ITypeDefOrRef type)
    {
        return type switch
        {
            TypeSpecification { Signature: GenericInstanceTypeSignature genericSig } => FormatGenericInstance(genericSig),
            TypeSpecification { Signature: { } signature } => GetExplicitInterfaceTypeName(signature),
            _ => GetMappedTypeName(type.Name?.ToString() ?? string.Empty) ?? RemoveGenericArity(type.FullName),
        };
    }

    private string GetExplicitInterfaceTypeName(TypeSignature signature)
    {
        return signature switch
        {
            GenericInstanceTypeSignature genericSig => FormatGenericInstance(genericSig),
            TypeDefOrRefSignature typeSig => GetExplicitInterfaceTypeName(typeSig.Type),
            _ => RemoveGenericArity(signature.FullName),
        };
    }

    private string FormatGenericInstance(GenericInstanceTypeSignature genericSig)
    {
        var genericTypeName = GetExplicitInterfaceTypeName(genericSig.GenericType);
        var tickIndex = genericTypeName.IndexOf('`', StringComparison.Ordinal);
        if (tickIndex >= 0)
        {
            genericTypeName = genericTypeName[..tickIndex];
        }

        var genericArgs = string.Join(", ", genericSig.TypeArguments.Select(GetExplicitInterfaceTypeName));

        return $"{genericTypeName}<{genericArgs}>";
    }

    private string? GetMappedExplicitInterfaceName(string oldName)
    {
        var splitName = oldName.Split('.');
        if (splitName.Length < 2)
        {
            return null;
        }

        var changedToken = false;
        for (var i = 0; i < splitName.Length - 1; i++)
        {
            if (!splitName[i].IsObfuscatedName())
            {
                continue;
            }

            var mappedTypeName = GetMappedTypeName(splitName[i]);
            if (mappedTypeName is null)
            {
                continue;
            }

            splitName[i] = mappedTypeName;
            changedToken = true;
        }

        return changedToken ? string.Join(".", splitName) : null;
    }

    private static string RemoveGenericArity(string typeName)
    {
        var result = new System.Text.StringBuilder(typeName.Length);

        for (var i = 0; i < typeName.Length; i++)
        {
            if (typeName[i] != '`')
            {
                result.Append(typeName[i]);
                continue;
            }

            var next = i + 1;
            while (next < typeName.Length && char.IsDigit(typeName[next]))
            {
                next++;
            }

            if (next == i + 1)
            {
                result.Append(typeName[i]);
                continue;
            }

            i = next - 1;
        }

        return result.ToString();
    }

    private static void SetMemberName(IMemberDefinition member, Utf8String name)
    {
        switch (member)
        {
            case MethodDefinition method:
                method.Name = name;
                break;
            case PropertyDefinition property:
                property.Name = name;
                break;
            case EventDefinition @event:
                @event.Name = name;
                break;
            default:
                throw new NotImplementedException(
                    $"Renaming explicit member type '{member.GetType().Name}' is not implemented."
                );
        }
    }
}
