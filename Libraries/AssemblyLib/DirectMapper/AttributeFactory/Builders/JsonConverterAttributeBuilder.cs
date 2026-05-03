using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.AttributeFactory.Builders;

[Injectable]
public class JsonConverterAttributeBuilder(ILogger<JsonConverterAttributeBuilder> logger, DataProvider dataProvider)
    : IAttributeBuilder
{
    public bool Enabled => true;

    public void Build()
    {
        var renameMap = new Dictionary<string, TypeDefinition>();
        foreach (var (fullName, mapping) in dataProvider.DirectMapModels)
        {
            renameMap.Add(fullName, mapping.ToolData.Type!);
        }

        var module = dataProvider.LoadedModule!;
        foreach (var type in module.GetAllTypes())
        {
            // Update attributes on the type itself
            UpdateJsonConvertersInTarget(type, renameMap);

            // Update attributes on properties
            foreach (var property in type.Properties)
            {
                UpdateJsonConvertersInTarget(property, renameMap);
            }

            // Update attributes on fields
            foreach (var field in type.Fields)
            {
                UpdateJsonConvertersInTarget(field, renameMap);
            }
        }
    }

    private void UpdateJsonConvertersInTarget(IHasCustomAttribute target, Dictionary<string, TypeDefinition> renameMap)
    {
        List<(CustomAttribute oldAttr, CustomAttribute newAttr)> replacements = [];

        foreach (var attr in target.CustomAttributes)
        {
            if (!attr.IsJsonConverterAttribute())
            {
                continue;
            }

            var referencedTypeName = attr.ExtractTypeNameFromAttribute();
            if (referencedTypeName is null)
            {
                continue;
            }

            // Try to find and update the attribute
            var newAttr = TryCreateUpdatedAttribute(attr, referencedTypeName, renameMap);

            if (newAttr != null)
            {
                logger.LogInformation("Successfully created updated attribute for: {TypeName}", referencedTypeName);
                replacements.Add((attr, newAttr));
            }
            else
            {
                logger.LogWarning(
                    "No update needed or failed to create updated attribute for: {TypeName}",
                    referencedTypeName
                );
            }
        }

        foreach (var (oldAttr, newAttr) in replacements)
        {
            target.CustomAttributes.Remove(oldAttr);
            target.CustomAttributes.Add(newAttr);
        }
    }

    private CustomAttribute? TryCreateUpdatedAttribute(
        CustomAttribute originalAttr,
        string referencedTypeName,
        Dictionary<string, TypeDefinition> renameMap
    )
    {
        // Check if it's a generic type
        if (referencedTypeName.Contains('<'))
        {
            return TryCreateGenericAttribute(originalAttr, renameMap);
        }

        // Simple type - direct lookup
        if (renameMap.TryGetValue(referencedTypeName, out var newTypeDef))
        {
            return CreateJsonConverterAttribute(newTypeDef);
        }

        return null;
    }

    private CustomAttribute? TryCreateGenericAttribute(
        CustomAttribute originalAttr,
        Dictionary<string, TypeDefinition> renameMap
    )
    {
        if (originalAttr.Signature?.FixedArguments.Count == 0)
        {
            logger.LogWarning("No fixed arguments in attribute signature");
            return null;
        }

        var argument = originalAttr.Signature?.FixedArguments[0];

        if (argument?.Element is not GenericInstanceTypeSignature genericSig)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Argument is not a GenericInstanceTypeSignature, it's: {Type}",
                    argument?.Element?.GetType().Name
                );
            }

            return null;
        }

        var genericTypeDef = genericSig.GenericType;
        var baseTypeName = genericTypeDef.FullName;

        // Check if the base generic type needs renaming
        var baseTypeRenamed = renameMap.TryGetValue(baseTypeName, out var newGenericTypeDef);

        // Try to update each generic argument
        var updatedArguments = new List<TypeSignature>();
        var anyArgumentUpdated = false;

        foreach (var typeSig in genericSig.TypeArguments)
        {
            var argFullName = typeSig.GetFullTypeName();

            // Try to match the OLD name from the string representation
            var updatedArg = FindReplacementForArgument(typeSig, argFullName, renameMap);

            if (updatedArg != null)
            {
                updatedArguments.Add(updatedArg);
                anyArgumentUpdated = true;
            }
            else
            {
                updatedArguments.Add(typeSig);
            }
        }

        // No changes needed
        if (!baseTypeRenamed && !anyArgumentUpdated)
        {
            return null;
        }

        TypeDefinition? finalBaseType;

        if (baseTypeRenamed)
        {
            finalBaseType = newGenericTypeDef;
        }
        else
        {
            genericTypeDef.Resolve(dataProvider.Context, out finalBaseType);
        }

        if (finalBaseType == null)
        {
            logger.LogError("Could not resolve base generic type");
            return null;
        }

        var result = CreateJsonConverterAttributeWithGeneric(finalBaseType, updatedArguments.ToArray());
        return result;
    }

    private TypeSignature? FindReplacementForArgument(
        TypeSignature arg,
        string argFullName,
        Dictionary<string, TypeDefinition> renameMap
    )
    {
        // Try to resolve normally first
        if (arg is TypeDefOrRefSignature typeDefOrRef)
        {
            typeDefOrRef.Type.Resolve(dataProvider.Context, out var typeDef);

            if (typeDef != null)
            {
                // Type is resolvable - check if it's directly renamed
                var fullName = typeDef.FullName;
                if (renameMap.TryGetValue(fullName, out var renamedType))
                {
                    return renamedType.ToTypeSignature();
                }

                if (typeDef.IsNested && typeDef.DeclaringType != null)
                {
                    // Try to find a rename-map-based replacement first.
                    // If not found, STILL return typeDef.ToTypeSignature() to replace
                    // any stale TypeReference in the blob with the current TypeDefinition.
                    // Without this, a TypeReference with the old declaring-type name
                    // (e.g. GClass3629+EActionType) is kept even after its renamed.
                    return HandleNestedType(typeDef, renameMap) ?? typeDef.ToTypeSignature();
                }
            }
        }

        // Handle nested types like "GClass3666+EDialogLineIconType"
        if (argFullName.Contains('+'))
        {
            var parts = argFullName.Split('+');
            if (parts.Length == 2)
            {
                var declaringTypeName = parts[0];
                var nestedTypeName = parts[1];

                // Remove namespace if present (e.g., "Some.Namespace.GClass3666" -> "GClass3666")
                var lastDot = declaringTypeName.LastIndexOf('.');
                if (lastDot >= 0)
                {
                    declaringTypeName = declaringTypeName.Substring(lastDot + 1);
                }

                // Look for the declaring type in rename map
                if (renameMap.TryGetValue(declaringTypeName, out var renamedDeclaringType))
                {
                    // Find the nested type
                    var newNestedType = renamedDeclaringType.NestedTypes.FirstOrDefault(nt =>
                        nt.Name == nestedTypeName
                    );

                    if (newNestedType != null)
                    {
                        return newNestedType.ToTypeSignature();
                    }
                }
            }
        }

        // Try direct lookup by full name
        if (renameMap.TryGetValue(argFullName, out var directMatch))
        {
            return directMatch.ToTypeSignature();
        }

        return null;
    }

    private static TypeSignature? HandleNestedType(TypeDefinition typeDef, Dictionary<string, TypeDefinition> renameMap)
    {
        var declaringTypeName = typeDef.DeclaringType!.Name?.ToString();
        var declaringTypeFullName = typeDef.DeclaringType.FullName;

        // Try simple name first
        if (declaringTypeName != null && renameMap.TryGetValue(declaringTypeName, out var renamedDeclaringType))
        {
            return FindNestedInParent(typeDef, renamedDeclaringType);
        }

        // Try full name
        if (renameMap.TryGetValue(declaringTypeFullName, out renamedDeclaringType))
        {
            return FindNestedInParent(typeDef, renamedDeclaringType);
        }

        return null;
    }

    private static TypeSignature? FindNestedInParent(TypeDefinition originalNested, TypeDefinition newParent)
    {
        var nestedTypeName = originalNested.Name?.ToString();
        if (nestedTypeName == null)
        {
            return null;
        }

        var newNestedType = newParent.NestedTypes.FirstOrDefault(nt => nt.Name == nestedTypeName);

        return newNestedType?.ToTypeSignature();
    }

    private CustomAttribute CreateJsonConverterAttributeWithGeneric(
        TypeDefinition converterType,
        TypeSignature[] genericArguments
    )
    {
        var module = dataProvider.LoadedModule!;
        var factory = module.CorLibTypeFactory;

        var sysTypeRef = factory
            .CorLibScope.CreateTypeReference("System", "Type")
            .ImportWith(module.DefaultImporter)
            .ToTypeSignature(false);

        var newtonsoftAssembly = module.AssemblyReferences.FirstOrDefault(a => a.Name == "Newtonsoft.Json");

        var jsonConverterAttrRef = new TypeReference(
            module,
            newtonsoftAssembly,
            "Newtonsoft.Json",
            "JsonConverterAttribute"
        )
            .CreateMemberReference(".ctor", MethodSignature.CreateInstance(factory.Void, [sysTypeRef]))
            .ImportWith(module.DefaultImporter);

        var scope =
            converterType.DeclaringType == null
                // IDE says we don't need this cast, but this is a lie. DO NOT REMOVE
                ? (IResolutionScope)module
                : (IResolutionScope)converterType.DeclaringType.ToTypeReference();

        // Create a proper type reference for the generic base type
        var converterTypeRef = new TypeReference(module, scope, converterType.Namespace, converterType.Name).ImportWith(
            module.DefaultImporter
        );

        var genericTypeSig = new GenericInstanceTypeSignature(
            converterTypeRef,
            converterType.IsValueType,
            genericArguments
        );

        var customAttribute = new CustomAttribute(jsonConverterAttrRef)
        {
            Signature = new CustomAttributeSignature(new CustomAttributeArgument(sysTypeRef, genericTypeSig)),
        };

        return customAttribute;
    }

    private CustomAttribute CreateJsonConverterAttribute(TypeDefinition converterType)
    {
        var module = dataProvider.LoadedModule;
        var factory = module!.CorLibTypeFactory;

        // Get System.Type reference
        var sysTypeRef = factory
            .CorLibScope.CreateTypeReference("System", "Type")
            .ImportWith(module.DefaultImporter)
            .ToTypeSignature(false);

        // Find the Newtonsoft.Json assembly reference
        var newtonsoftAssembly = module.AssemblyReferences.FirstOrDefault(a => a.Name == "Newtonsoft.Json");

        // Create JsonConverterAttribute constructor reference from the correct assembly
        var jsonConverterAttrRef = new TypeReference(
            module,
            newtonsoftAssembly,
            "Newtonsoft.Json",
            "JsonConverterAttribute"
        )
            .CreateMemberReference(".ctor", MethodSignature.CreateInstance(factory.Void, [sysTypeRef]))
            .ImportWith(module.DefaultImporter);

        var customAttribute = new CustomAttribute(jsonConverterAttrRef)
        {
            Signature = new CustomAttributeSignature(
                new CustomAttributeArgument(sysTypeRef, converterType.ToTypeSignature())
            ),
        };

        return customAttribute;
    }
}
