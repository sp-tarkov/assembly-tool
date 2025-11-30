using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable]
public class AttributeFactory(DataProvider dataProvider)
{
    public void UpdateAsyncAttributes()
    {
        var types = dataProvider.DirectMapModels.Select(r => r.Value.ToolData.Type).ToList();
        var nestedTypes = new List<TypeDefinition>();
        foreach (var type in types)
        {
            nestedTypes.AddRange(type?.NestedTypes ?? []);
        }
        types.AddRange(nestedTypes);

        foreach (var type in types)
        {
            if (type is null || type.NestedTypes.Count == 0)
            {
                continue;
            }

            foreach (var method in type.Methods)
            {
                if (IsAsyncMethod(method))
                {
                    UpdateAsyncAttribute(method, type.NestedTypes);
                }
            }
        }
    }

    /// <summary>
    ///     Updates all JsonConverter attributes in a module based on a rename mapping
    /// </summary>
    public void UpdateAllJsonConverterAttributes(Dictionary<string, TypeDefinition> renameMap)
    {
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

    /// <summary>
    ///     Updates all TypeConverter attributes in a module based on a rename mapping
    /// </summary>
    public void UpdateAllTypeConverterAttributes(Dictionary<string, TypeDefinition> renameMap)
    {
        var module = dataProvider.LoadedModule!;
        foreach (var type in module.GetAllTypes())
        {
            // Update attributes on the type itself
            UpdateTypeConvertersInTarget(type, renameMap);

            // Update attributes on properties
            foreach (var property in type.Properties)
            {
                UpdateTypeConvertersInTarget(property, renameMap);
            }

            // Update attributes on fields
            foreach (var field in type.Fields)
            {
                UpdateTypeConvertersInTarget(field, renameMap);
            }
        }
    }

    private void UpdateAsyncAttribute(MethodDefinition method, IList<TypeDefinition> nestedTypes)
    {
        // Key - Old :: Val - New
        Dictionary<CustomAttribute, CustomAttribute> attrReplacements = [];

        foreach (var attr in method.CustomAttributes.ToArray())
        {
            if (!IsAsyncStateMachineAttribute(attr))
            {
                continue;
            }

            // Find the argument target in the nested types
            var typeDefTarget = nestedTypes.FirstOrDefault(t =>
                t.Name == ((TypeDefOrRefSignature)attr.Signature?.FixedArguments[0].Element!).Name
            );

            if (typeDefTarget is null)
            {
                Log.Error(
                    "Failed to locate AsyncStateMachineAttribute for method {DeclaringTypeName}::{MethodName}",
                    method.DeclaringType?.Name?.ToString(),
                    method.Name?.ToString()
                );
                continue;
            }

            attrReplacements.Add(attr, CreateNewAsyncAttribute(typeDefTarget));
        }

        foreach (var replacement in attrReplacements)
        {
            Log.Information(
                "Updating AsyncStateMachineAttribute for method {DeclaringTypeName}::{MethodName}",
                method.DeclaringType?.Name?.ToString(),
                method.Name?.ToString()
            );

            method.CustomAttributes.Remove(replacement.Key);
            method.CustomAttributes.Add(replacement.Value);
        }
    }

    private CustomAttribute CreateNewAsyncAttribute(TypeDefinition targetTypeDef)
    {
        var module = dataProvider.LoadedModule;
        var factory = module!.CorLibTypeFactory;

        var sysTypeRef = factory
            .CorLibScope.CreateTypeReference("System", "Type")
            .ImportWith(module.DefaultImporter)
            .ToTypeSignature();

        var asyncAttrRef = factory
            .CorLibScope.CreateTypeReference("System.Runtime.CompilerServices", "AsyncStateMachineAttribute")
            .CreateMemberReference(".ctor", MethodSignature.CreateInstance(module.CorLibTypeFactory.Void, sysTypeRef))
            .ImportWith(module.DefaultImporter);

        // Create a custom attribute.
        var customAttribute = new CustomAttribute(asyncAttrRef);

        var targetSig = targetTypeDef.ToTypeSignature();

        customAttribute.Signature?.FixedArguments.Add(new CustomAttributeArgument(sysTypeRef, targetSig));

        return customAttribute;
    }

    private void UpdateJsonConvertersInTarget(IHasCustomAttribute target, Dictionary<string, TypeDefinition> renameMap)
    {
        List<(CustomAttribute oldAttr, CustomAttribute newAttr)> replacements = [];

        foreach (var attr in target.CustomAttributes)
        {
            if (!IsJsonConverterAttribute(attr))
            {
                continue;
            }

            /*
            var referencedTypeName = ExtractTypeNameFromAttribute(attr);
            if (referencedTypeName != null && renameMap.TryGetValue(referencedTypeName, out var newTypeDef))
            {
                replacements.Add((attr, CreateJsonConverterAttribute(newTypeDef)));
            }*/

            var referencedTypeName = ExtractTypeNameFromAttribute(attr);
            if (referencedTypeName is null)
            {
                continue;
            }

            // Try to find and update the attribute
            var newAttr = TryCreateUpdatedAttribute(attr, referencedTypeName, renameMap);

            if (newAttr != null)
            {
                Log.Information("Successfully created updated attribute for: {TypeName}", referencedTypeName);
                replacements.Add((attr, newAttr));
            }
            else
            {
                Log.Information(
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

    /// <summary>
    /// Attempts to update a type signature if it or its declaring type is in the rename map
    /// Handles nested types like GClass3666.EDialogLineIconType where GClass3666 is renamed
    /// </summary>
    private static TypeSignature? TryUpdateTypeSignature(
        TypeSignature typeSig,
        Dictionary<string, TypeDefinition> renameMap
    )
    {
        if (typeSig is not TypeDefOrRefSignature typeDefOrRef)
        {
            return null;
        }

        var typeDef = typeDefOrRef.Type?.Resolve();
        if (typeDef == null)
        {
            return null;
        }

        // Check if the type itself is being renamed
        var fullName = typeDef.FullName;
        if (renameMap.TryGetValue(fullName, out var directRenamedType))
        {
            return directRenamedType.ToTypeSignature();
        }

        // Check if this is a nested type and its declaring type is being renamed
        if (typeDef.IsNested && typeDef.DeclaringType != null)
        {
            var declaringTypeFullName = typeDef.DeclaringType.FullName;

            if (renameMap.TryGetValue(declaringTypeFullName, out var renamedDeclaringType))
            {
                // Find the corresponding nested type in the renamed declaring type
                var nestedTypeName = typeDef.Name?.ToString();
                if (nestedTypeName != null)
                {
                    var correspondingNestedType = renamedDeclaringType.NestedTypes.FirstOrDefault(nt =>
                        nt.Name == nestedTypeName
                    );

                    if (correspondingNestedType != null)
                    {
                        return correspondingNestedType.ToTypeSignature();
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractTypeNameFromAttribute(CustomAttribute attr)
    {
        if (attr.Signature?.FixedArguments.Count == 0)
        {
            return null;
        }

        var argument = attr.Signature?.FixedArguments[0];

        return argument?.Element switch
        {
            TypeDefOrRefSignature typeSig => GetFullTypeName(typeSig),
            ITypeDescriptor typeDesc => typeDesc.FullName,
            _ => null,
        };
    }

    private static string GetFullTypeName(TypeSignature typeSig)
    {
        switch (typeSig)
        {
            // Handle generic types like GClass1866<GClass3666.EDialogLineIconType>
            case GenericInstanceTypeSignature genericSig:
            {
                var baseTypeName = genericSig.GenericType.FullName;
                var genericArgs = string.Join(", ", genericSig.TypeArguments.Select(GetFullTypeName));
                return $"{baseTypeName}<{genericArgs}>";
            }
            // Handle nested types
            case TypeDefOrRefSignature typeDefOrRef:
                return typeDefOrRef.FullName;
            default:
                return typeSig.FullName;
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
        // Extract the original type signature from the attribute
        if (originalAttr.Signature?.FixedArguments.Count == 0)
        {
            Log.Information("No fixed arguments in attribute signature");
            return null;
        }

        var argument = originalAttr.Signature?.FixedArguments[0];

        if (argument?.Element is not GenericInstanceTypeSignature genericSig)
        {
            Log.Debug(
                "Argument is not a GenericInstanceTypeSignature, it's: {Type}",
                argument?.Element?.GetType().Name
            );
            return null;
        }

        // Get the generic type definition name
        var genericTypeDef = genericSig.GenericType;
        var baseTypeName = genericTypeDef.FullName;

        Log.Information(
            "Generic type base: {BaseTypeName}, Arguments: {ArgCount}",
            baseTypeName,
            genericSig.TypeArguments.Count
        );

        foreach (var arg in genericSig.TypeArguments)
        {
            Log.Debug("  Generic argument: {ArgType} (FullName: {FullName})", arg.GetType().Name, GetFullTypeName(arg));
        }

        // Check if the generic type itself needs to be renamed
        var genericTypeRenamed = renameMap.TryGetValue(baseTypeName, out var newGenericTypeDef);

        // Update generic arguments if any are renamed
        var updatedArguments = UpdateGenericArguments(genericSig.TypeArguments, renameMap);

        if (!genericTypeRenamed && updatedArguments == null)
        {
            Log.Debug("Neither generic type nor arguments were renamed");
            return null;
        }

        // Use the original type def if not renamed
        if (!genericTypeRenamed)
        {
            newGenericTypeDef = genericTypeDef.Resolve();
            if (newGenericTypeDef == null)
            {
                Log.Debug("Could not resolve generic type definition");
                return null;
            }
        }

        // Use updated arguments if available, otherwise use original
        var finalArguments = updatedArguments ?? genericSig.TypeArguments.ToArray();

        Log.Debug("Creating updated generic attribute with {ArgCount} arguments", finalArguments.Length);
        return CreateJsonConverterAttributeWithGeneric(newGenericTypeDef!, finalArguments);
    }

    /// <summary>
    ///     Updates generic type arguments based on the rename map
    /// Returns null if no arguments were changed
    /// </summary>
    private static TypeSignature[]? UpdateGenericArguments(
        IList<TypeSignature> originalArguments,
        Dictionary<string, TypeDefinition> renameMap
    )
    {
        TypeSignature[]? updatedArguments = null;
        var anyChanged = false;

        for (var i = 0; i < originalArguments.Count; i++)
        {
            var arg = originalArguments[i];
            var updatedArg = TryUpdateTypeSignature(arg, renameMap);

            if (updatedArg != null)
            {
                // Lazy initialize the array only if we find a change
                if (updatedArguments == null)
                {
                    updatedArguments = new TypeSignature[originalArguments.Count];
                    // Copy existing arguments up to this point
                    for (var j = 0; j < i; j++)
                    {
                        updatedArguments[j] = originalArguments[j];
                    }
                }

                updatedArguments[i] = updatedArg;
                anyChanged = true;
            }
            else
            {
                // Copy unchanged argument
                updatedArguments?[i] = arg;
            }
        }

        return anyChanged ? updatedArguments : null;
    }

    private CustomAttribute CreateJsonConverterAttribute(TypeDefinition converterType)
    {
        var module = dataProvider.LoadedModule;
        var factory = module!.CorLibTypeFactory;

        // Get System.Type reference
        var sysTypeRef = factory
            .CorLibScope.CreateTypeReference("System", "Type")
            .ImportWith(module.DefaultImporter)
            .ToTypeSignature();

        // Find the Newtonsoft.Json assembly reference
        var newtonsoftAssembly = module.AssemblyReferences.FirstOrDefault(a => a.Name == "Newtonsoft.Json");

        // Create JsonConverterAttribute constructor reference from the correct assembly
        var jsonConverterAttrRef = new TypeReference(
            module,
            newtonsoftAssembly,
            "Newtonsoft.Json",
            "JsonConverterAttribute"
        )
            .CreateMemberReference(".ctor", MethodSignature.CreateInstance(factory.Void, sysTypeRef))
            .ImportWith(module.DefaultImporter);

        var customAttribute = new CustomAttribute(jsonConverterAttrRef)
        {
            Signature = new CustomAttributeSignature(
                new CustomAttributeArgument(sysTypeRef, converterType.ToTypeSignature())
            ),
        };

        return customAttribute;
    }

    private CustomAttribute CreateJsonConverterAttributeWithGeneric(
        TypeDefinition converterType,
        TypeSignature[] genericArguments
    )
    {
        var module = dataProvider.LoadedModule;
        var factory = module!.CorLibTypeFactory;

        // Get System.Type reference
        var sysTypeRef = factory
            .CorLibScope.CreateTypeReference("System", "Type")
            .ImportWith(module.DefaultImporter)
            .ToTypeSignature();

        // Find the Newtonsoft.Json assembly reference
        var newtonsoftAssembly = module.AssemblyReferences.FirstOrDefault(a => a.Name == "Newtonsoft.Json");

        // Create JsonConverterAttribute constructor reference
        var jsonConverterAttrRef = new TypeReference(
            module,
            newtonsoftAssembly,
            "Newtonsoft.Json",
            "JsonConverterAttribute"
        )
            .CreateMemberReference(".ctor", MethodSignature.CreateInstance(factory.Void, sysTypeRef))
            .ImportWith(module.DefaultImporter);

        Log.Information("HERE");

        // Build the closed generic type signature
        var genericTypeSig = new GenericInstanceTypeSignature(
            converterType.ToTypeReference(),
            converterType.IsValueType,
            genericArguments
        );

        var customAttribute = new CustomAttribute(jsonConverterAttrRef)
        {
            Signature = new CustomAttributeSignature(new CustomAttributeArgument(sysTypeRef, genericTypeSig)),
        };

        return customAttribute;
    }

    private void UpdateTypeConvertersInTarget(IHasCustomAttribute target, Dictionary<string, TypeDefinition> renameMap)
    {
        List<(CustomAttribute oldAttr, CustomAttribute newAttr)> replacements = [];

        foreach (var attr in target.CustomAttributes)
        {
            if (!IsTypeConverterAttribute(attr))
            {
                continue;
            }

            var referencedTypeName = ExtractTypeNameFromAttribute(attr);
            if (referencedTypeName != null && renameMap.TryGetValue(referencedTypeName, out var newTypeDef))
            {
                replacements.Add((attr, CreateTypeConverterAttribute(newTypeDef)));
            }
        }

        foreach (var (oldAttr, newAttr) in replacements)
        {
            target.CustomAttributes.Remove(oldAttr);
            target.CustomAttributes.Add(newAttr);
        }
    }

    private CustomAttribute CreateTypeConverterAttribute(TypeDefinition converterType)
    {
        var module = dataProvider.LoadedModule;
        var factory = module!.CorLibTypeFactory;

        // Get System.Type reference
        var sysTypeRef = factory
            .CorLibScope.CreateTypeReference("System", "Type")
            .ImportWith(module.DefaultImporter)
            .ToTypeSignature();

        // TypeConverterAttribute is in System.ComponentModel which is part of System
        // Find the System assembly reference (or System.ComponentModel if it's separate)
        var systemAssembly = module.AssemblyReferences.FirstOrDefault(a =>
            a.Name == "System" || a.Name == "System.ComponentModel" || a.Name == "System.ComponentModel.TypeConverter"
        );

        if (systemAssembly == null)
        {
            // Fallback to creating System reference
            systemAssembly = new AssemblyReference("System", new Version(4, 0, 0, 0));
            module.AssemblyReferences.Add(systemAssembly);
        }

        // Create TypeConverterAttribute constructor reference from the correct assembly
        var typeConverterAttrRef = new TypeReference(
            module,
            systemAssembly,
            "System.ComponentModel",
            "TypeConverterAttribute"
        )
            .CreateMemberReference(".ctor", MethodSignature.CreateInstance(factory.Void, sysTypeRef))
            .ImportWith(module.DefaultImporter);

        var customAttribute = new CustomAttribute(typeConverterAttrRef)
        {
            Signature = new CustomAttributeSignature(
                new CustomAttributeArgument(sysTypeRef, converterType.ToTypeSignature())
            ),
        };

        return customAttribute;
    }

    private static bool IsAsyncMethod(MethodDefinition method)
    {
        return method
            .CustomAttributes.Select(s => s.Constructor?.DeclaringType?.FullName)
            .Contains("System.Runtime.CompilerServices.AsyncStateMachineAttribute");
    }

    private static bool IsAsyncStateMachineAttribute(CustomAttribute attr)
    {
        return attr.Constructor?.DeclaringType?.FullName
            == "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
    }

    private static bool IsJsonConverterAttribute(CustomAttribute attr)
    {
        var fullName = attr.Constructor?.DeclaringType?.FullName;
        return fullName == "Newtonsoft.Json.JsonConverterAttribute";
    }

    private static bool IsTypeConverterAttribute(CustomAttribute attr)
    {
        var fullName = attr.Constructor?.DeclaringType?.FullName;
        return fullName == "System.ComponentModel.TypeConverterAttribute";
    }
}
