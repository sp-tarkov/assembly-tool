using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AssemblyLib.Models;
using AssemblyLib.Shared;
using Serilog;
using Serilog.Events;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable]
public class AttributeFactory(DataProvider dataProvider)
{
    public void UpdateAsyncAttributes()
    {
        var types = dataProvider.LoadedModule.GetAllTypes();

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
            .ToTypeSignature(false);

        var asyncAttrRef = factory
            .CorLibScope.CreateTypeReference("System.Runtime.CompilerServices", "AsyncStateMachineAttribute")
            .CreateMemberReference(".ctor", MethodSignature.CreateInstance(module.CorLibTypeFactory.Void, [sysTypeRef]))
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

            var referencedTypeName = ExtractTypeNameFromAttribute(attr);
            if (referencedTypeName is null)
            {
                continue;
            }

            // Try to find and update the attribute
            var newAttr = TryCreateUpdatedAttribute(attr, referencedTypeName, renameMap);

            if (newAttr != null)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Successfully created updated attribute for: {TypeName}", referencedTypeName);
                }

                replacements.Add((attr, newAttr));
            }
            else
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug(
                        "No update needed or failed to create updated attribute for: {TypeName}",
                        referencedTypeName
                    );
                }
            }
        }

        foreach (var (oldAttr, newAttr) in replacements)
        {
            target.CustomAttributes.Remove(oldAttr);
            target.CustomAttributes.Add(newAttr);
        }
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
            case GenericInstanceTypeSignature genericSig:
            {
                var baseTypeName = genericSig.GenericType.FullName;
                var genericArgs = string.Join(", ", genericSig.TypeArguments.Select(GetFullTypeName));
                return $"{baseTypeName}<{genericArgs}>";
            }
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
        if (originalAttr.Signature?.FixedArguments.Count == 0)
        {
            Log.Warning("No fixed arguments in attribute signature");
            return null;
        }

        var argument = originalAttr.Signature?.FixedArguments[0];

        if (argument?.Element is not GenericInstanceTypeSignature genericSig)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
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

        foreach (var arg in genericSig.TypeArguments)
        {
            var argFullName = GetFullTypeName(arg);
            //Log.Information("  Processing argument: {ArgName}", argFullName);

            // THE KEY: Try to match the OLD name from the string representation
            var updatedArg = FindReplacementForArgument(arg, argFullName, renameMap);

            if (updatedArg != null)
            {
                /*
                Log.Information("    -> Updated to: {NewName}", GetFullTypeName(updatedArg));
                Log.Information("    -> Type details: {Details}", updatedArg.FullName);


                // Debug: Check what the TypeDefinition looks like
                if (updatedArg is TypeDefOrRefSignature tdr)
                {
                    var resolved = tdr.Type?.Resolve();
                    if (resolved != null)
                    {
                        Log.Information(
                            "    -> Resolved type name: {Name}, DeclaringType: {Declaring}",
                            resolved.Name.ToString(),
                            resolved.DeclaringType?.Name.ToString()
                        );

                    }
                }
                */

                updatedArguments.Add(updatedArg);
                anyArgumentUpdated = true;
            }
            else
            {
                //Log.Information("    -> No update needed");
                updatedArguments.Add(arg);
            }
        }

        if (!baseTypeRenamed && !anyArgumentUpdated)
        {
            //Log.Information("No changes needed for this generic attribute");
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
            Log.Error("Could not resolve base generic type");
            return null;
        }

        /*
        Log.Information("Creating new attribute with updated generic type");
        Log.Information("  Base type: {BaseType}", finalBaseType.FullName);
        Log.Information("  Arguments: {Args}", string.Join(", ", updatedArguments.Select(GetFullTypeName)));
        */

        var result = CreateJsonConverterAttributeWithGeneric(finalBaseType, updatedArguments.ToArray());

        /*
        // Verify what we created
        if (result.Signature?.FixedArguments.Count > 0)
        {
            var createdArg = result.Signature.FixedArguments[0];
            if (createdArg.Element is GenericInstanceTypeSignature createdGeneric)
            {
                Log.Information(
                    "  CREATED attribute with: {BaseType}<{Args}>",
                    createdGeneric.GenericType.FullName,
                    string.Join(", ", createdGeneric.TypeArguments.Select(GetFullTypeName))
                );
            }
        }
        */

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
                // Type is resolvable - check if it's in rename map
                var fullName = typeDef.FullName;
                if (renameMap.TryGetValue(fullName, out var renamedType))
                {
                    Log.Information("    Found direct match (resolvable): {Old} -> {New}", fullName, renamedType.Name);
                    return renamedType.ToTypeSignature();
                }

                // Check for nested types
                if (typeDef.IsNested && typeDef.DeclaringType != null)
                {
                    return HandleNestedType(typeDef, renameMap);
                }
            }
        }

        // Type couldn't be resolved - it's probably already renamed
        // Parse the string name to extract the old declaring type name
        //Log.Information("    Type not resolvable, parsing string: {Name}", argFullName);

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

                /*
                Log.Information(
                    "    Parsed nested: declaring={Declaring}, nested={Nested}",
                    declaringTypeName,
                    nestedTypeName
                );
                */

                // Look for the declaring type in rename map
                if (renameMap.TryGetValue(declaringTypeName, out var renamedDeclaringType))
                {
                    /*
                    Log.Information(
                        "    Found OLD declaring type in renameMap: {Old} -> {New}",
                        declaringTypeName,
                        renamedDeclaringType.Name.ToString()
                    );
                    */

                    // Find the nested type
                    var newNestedType = renamedDeclaringType.NestedTypes.FirstOrDefault(nt =>
                        nt.Name == nestedTypeName
                    );

                    if (newNestedType != null)
                    {
                        //Log.Information("    Found nested type in renamed parent: {Nested}", nestedTypeName);
                        return newNestedType.ToTypeSignature();
                    }

                    /*
                    Log.Warning(
                        "    Nested type {Nested} not found in {Parent}. Available: {Available}",
                        nestedTypeName,
                        renamedDeclaringType.Name,
                        string.Join(", ", renamedDeclaringType.NestedTypes.Select(nt => nt.Name?.ToString()))
                    );
                    */
                }
                else
                {
                    //Log.Warning("    Declaring type {Declaring} not found in renameMap", declaringTypeName);

                    // Show what keys exist that might match
                    var similarKeys = renameMap
                        .Keys.Where(k =>
                            k.Contains(declaringTypeName.Substring(0, Math.Min(declaringTypeName.Length, 8)))
                        )
                        .ToList();

                    /*
                    if (similarKeys.Any())
                    {
                        Log.Information("    Similar keys: {Keys}", string.Join(", ", similarKeys.Take(5)));
                    }
                    */
                }
            }
        }

        // Try direct lookup by full name
        if (renameMap.TryGetValue(argFullName, out var directMatch))
        {
            //Log.Information("    Found direct string match: {Name}", argFullName);
            return directMatch.ToTypeSignature();
        }

        return null;
    }

    private static TypeSignature? HandleNestedType(TypeDefinition typeDef, Dictionary<string, TypeDefinition> renameMap)
    {
        var declaringTypeName = typeDef.DeclaringType!.Name?.ToString();
        var declaringTypeFullName = typeDef.DeclaringType.FullName;

        /*
        Log.Information(
            "    Nested type - declaring: {DeclaringName} (full: {DeclaringFull})",
            declaringTypeName,
            declaringTypeFullName
        );
        */

        // Try simple name first
        if (declaringTypeName != null && renameMap.TryGetValue(declaringTypeName, out var renamedDeclaringType))
        {
            Log.Information(
                "    Found declaring type by NAME: {Old} -> {New}",
                declaringTypeName,
                renamedDeclaringType.Name
            );
            return FindNestedInParent(typeDef, renamedDeclaringType);
        }

        // Try full name
        if (renameMap.TryGetValue(declaringTypeFullName, out renamedDeclaringType))
        {
            /*
            Log.Information(
                "    Found declaring type by FULLNAME: {Old} -> {New}",
                declaringTypeFullName,
                renamedDeclaringType.Name
            );
            */

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

        if (newNestedType != null)
        {
            Log.Information("    Found nested type: {NestedName}", nestedTypeName);
            return newNestedType.ToTypeSignature();
        }

        Log.Warning(
            "    Nested type {NestedName} not found in {DeclaringType}. Available: {Available}",
            nestedTypeName,
            newParent.Name,
            string.Join(", ", newParent.NestedTypes.Select(nt => nt.Name?.ToString()))
        );

        return null;
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
                ? (IResolutionScope)module
                : (IResolutionScope)converterType.DeclaringType.ToTypeReference();

        // Create a proper type reference for the generic base type
        var converterTypeRef = new TypeReference(module, scope, converterType.Namespace, converterType.Name).ImportWith(
            module.DefaultImporter
        );

        /*
        Log.Information("  Creating GenericInstanceTypeSignature:");
        Log.Information("    Base: {Base}", converterTypeRef.FullName);
        Log.Information("    Args: {Args}", string.Join(", ", genericArguments.Select(a => a.FullName)));
        */

        var genericTypeSig = new GenericInstanceTypeSignature(
            converterTypeRef,
            converterType.IsValueType,
            genericArguments
        );

        //Log.Information("  GenericInstanceTypeSignature created: {Sig}", genericTypeSig.FullName);

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
            .ToTypeSignature(false);

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
            .CreateMemberReference(".ctor", MethodSignature.CreateInstance(factory.Void, [sysTypeRef]))
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
