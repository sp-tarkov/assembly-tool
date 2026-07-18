using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AssemblyLib.Helpers;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory.SigRenamers;

[Injectable]
public class CompilerGeneratedSigRenamer(
    ILogger<CompilerGeneratedSigRenamer> logger,
    DataProvider dataProvider,
    MemberReferenceCache memberReferenceCache,
    DirectRenameCache directRenameCache
) : ISigRenamer
{
    private readonly HashSet<TypeDefinition> _processedTypes = [];
    private ReferenceIndex? _referenceIndex;

    public int Priority => -10;
    public bool Enabled => true;
    public ERenamerType Type => ERenamerType.CompilerGenerated;

    public void Rename(TypeDefinition targetType, TypeDefinition dummyType)
    {
        if (!_processedTypes.Add(targetType))
        {
            return;
        }

        RenameCompilerGeneratedTypes(targetType);
        RenameCompilerGeneratedMethods(targetType);
    }

    private void RenameCompilerGeneratedTypes(TypeDefinition targetType)
    {
        var referenceIndex = GetReferenceIndex();

        foreach (var generatedType in GetCompilerGeneratedTypes(targetType))
        {
            var sourceMethod = GetUniqueReferencingMethod(referenceIndex.TypeReferences, generatedType);
            if (sourceMethod?.Name is null)
            {
                PrefixCompilerGeneratedTypeName(generatedType);
                continue;
            }

            var newName = GetUniqueTypeName(
                generatedType,
                $"CG_{GetMethodNamePrefix(sourceMethod)}"
            );

            if (newName is null)
            {
                PrefixCompilerGeneratedTypeName(generatedType);
                continue;
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Renaming compiler generated type: {old} -> {new}",
                    generatedType.FullName,
                    newName
                );
            }

            generatedType.Name = new Utf8String(newName);
        }
    }

    private void RenameCompilerGeneratedMethods(TypeDefinition targetType)
    {
        var referenceIndex = GetReferenceIndex();

        foreach (var generatedMethod in GetCompilerGeneratedMethods(targetType))
        {
            var sourceMethod = GetUniqueReferencingMethod(referenceIndex.MethodReferences, generatedMethod);
            if (sourceMethod?.Name is null)
            {
                PrefixCompilerGeneratedMethodName(generatedMethod);
                continue;
            }

            var newName = GetUniqueMethodName(generatedMethod, $"CG_{GetMethodNamePrefix(sourceMethod)}");
            if (newName is null)
            {
                PrefixCompilerGeneratedMethodName(generatedMethod);
                continue;
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Renaming compiler generated method: {old} -> {new}",
                    generatedMethod.FullName,
                    newName
                );
            }

            var utf8Name = new Utf8String(newName);
            generatedMethod.Name = utf8Name;
            UpdateMethodMemberReferences(generatedMethod, utf8Name);
        }
    }

    private void PrefixCompilerGeneratedTypeName(TypeDefinition generatedType)
    {
        var currentName = generatedType.Name?.ToString();
        if (
            string.IsNullOrEmpty(currentName)
            || currentName.StartsWith("CG_", StringComparison.Ordinal)
            || !currentName.IsObfuscatedName()
        )
        {
            return;
        }

        var newName = GetUniqueTypeName(generatedType, $"CG_{RemoveGenericArity(currentName)}");
        if (newName is null)
        {
            return;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Prefixing compiler generated type: {old} -> {new}",
                generatedType.FullName,
                newName
            );
        }

        generatedType.Name = new Utf8String(newName);
    }

    private void PrefixCompilerGeneratedMethodName(MethodDefinition generatedMethod)
    {
        var currentName = generatedMethod.Name?.ToString();
        if (
            string.IsNullOrEmpty(currentName)
            || currentName.StartsWith("CG_", StringComparison.Ordinal)
            || !currentName.IsObfuscatedName()
        )
        {
            return;
        }

        var newName = GetUniqueMethodName(generatedMethod, $"CG_{currentName}");
        if (newName is null)
        {
            return;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Prefixing compiler generated method: {old} -> {new}",
                generatedMethod.FullName,
                newName
            );
        }

        var utf8Name = new Utf8String(newName);
        generatedMethod.Name = utf8Name;
        UpdateMethodMemberReferences(generatedMethod, utf8Name);
    }

    private IEnumerable<TypeDefinition> GetCompilerGeneratedTypes(TypeDefinition targetType)
    {
        var stack = new Stack<TypeDefinition>(targetType.NestedTypes);

        while (stack.TryPop(out var type))
        {
            foreach (var nestedType in type.NestedTypes)
            {
                stack.Push(nestedType);
            }

            if (
                type.IsCompilerGenerated()
                && IsCompilerGeneratedTypeCandidate(type)
                && !directRenameCache.Contains(type)
            )
            {
                yield return type;
            }
        }
    }

    private IEnumerable<MethodDefinition> GetCompilerGeneratedMethods(TypeDefinition targetType)
    {
        foreach (
            var method in targetType.Methods.Where(method =>
                IsCompilerGeneratedMethodCandidate(method) && !directRenameCache.Contains(method)
            )
        )
        {
            yield return method;
        }

        foreach (var type in GetCompilerGeneratedTypes(targetType))
        {
            foreach (
                var method in type.Methods.Where(method =>
                    IsCompilerGeneratedMethodCandidate(method) && !directRenameCache.Contains(method)
                )
            )
            {
                yield return method;
            }
        }
    }

    private static MethodDefinition? GetUniqueReferencingMethod<TKey>(
        IReadOnlyDictionary<TKey, HashSet<MethodDefinition>> references,
        TKey generatedMember
    )
        where TKey : notnull
    {
        if (!references.TryGetValue(generatedMember, out var matches))
        {
            return null;
        }

        return matches.Count == 1 ? matches.First() : null;
    }

    private ReferenceIndex GetReferenceIndex()
    {
        return _referenceIndex ??= BuildReferenceIndex();
    }

    private ReferenceIndex BuildReferenceIndex()
    {
        var referenceIndex = new ReferenceIndex();

        foreach (var method in GetReferenceSourceMethods())
        {
            AddMethodSignatureReferences(method, method.Signature, referenceIndex);

            if (method.CilMethodBody is null)
            {
                continue;
            }

            foreach (var local in method.CilMethodBody.LocalVariables)
            {
                AddTypeSignatureReferences(method, local.VariableType, referenceIndex);
            }

            foreach (var instruction in method.CilMethodBody.Instructions)
            {
                AddOperandReferences(method, instruction.Operand, referenceIndex);
            }
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Indexed compiler generated references: {typeCount} type(s), {methodCount} method(s)",
                referenceIndex.TypeReferences.Count,
                referenceIndex.MethodReferences.Count
            );
        }

        return referenceIndex;
    }

    private IEnumerable<MethodDefinition> GetReferenceSourceMethods()
    {
        return dataProvider
            .LoadedModule!
            .GetAllTypes()
            .Where(type => !type.IsCompilerGenerated())
            .SelectMany(type => type.Methods)
            .Where(IsReferenceSourceMethod);
    }

    private void AddOperandReferences(
        MethodDefinition sourceMethod,
        object? operand,
        ReferenceIndex referenceIndex
    )
    {
        switch (operand)
        {
            case null:
                return;
            case TypeSignature signature:
                AddTypeSignatureReferences(sourceMethod, signature, referenceIndex);
                return;
            case ITypeDefOrRef type:
                AddTypeReference(sourceMethod, type, referenceIndex);
                return;
            case FieldDefinition field:
                AddTypeReference(sourceMethod, field.DeclaringType, referenceIndex);
                AddFieldSignatureReferences(sourceMethod, field.Signature, referenceIndex);
                return;
            case MethodDefinition method:
                AddMethodReference(sourceMethod, method, referenceIndex);
                return;
            case MemberReference member:
                AddMemberReference(sourceMethod, member, referenceIndex);
                return;
            case MethodSpecification methodSpec:
                AddOperandReferences(sourceMethod, methodSpec.Method, referenceIndex);
                foreach (var typeArg in methodSpec.Signature?.TypeArguments ?? [])
                {
                    AddTypeSignatureReferences(sourceMethod, typeArg, referenceIndex);
                }
                return;
        }
    }

    private void AddMemberReference(
        MethodDefinition sourceMethod,
        MemberReference member,
        ReferenceIndex referenceIndex
    )
    {
        AddTypeReference(sourceMethod, member.DeclaringType, referenceIndex);

        if (member.Signature is MethodSignatureBase methodSignature)
        {
            AddMethodSignatureReferences(sourceMethod, methodSignature, referenceIndex);
        }
        else if (member.Signature is FieldSignature fieldSignature)
        {
            AddFieldSignatureReferences(sourceMethod, fieldSignature, referenceIndex);
        }

        if (!member.TryResolve(dataProvider.Context, out var resolved))
        {
            return;
        }

        switch (resolved)
        {
            case MethodDefinition method:
                AddMethodReference(sourceMethod, method, referenceIndex);
                break;
            case FieldDefinition field:
                AddTypeReference(sourceMethod, field.DeclaringType, referenceIndex);
                AddFieldSignatureReferences(sourceMethod, field.Signature, referenceIndex);
                break;
            case PropertyDefinition property:
                AddTypeReference(sourceMethod, property.DeclaringType, referenceIndex);
                break;
        }
    }

    private void AddMethodReference(
        MethodDefinition sourceMethod,
        MethodDefinition referencedMethod,
        ReferenceIndex referenceIndex
    )
    {
        AddTypeReference(sourceMethod, referencedMethod.DeclaringType, referenceIndex);
        AddMethodSignatureReferences(sourceMethod, referencedMethod.Signature, referenceIndex);

        if (!IsCompilerGeneratedMethodCandidate(referencedMethod))
        {
            return;
        }

        AddReference(referenceIndex.MethodReferences, referencedMethod, sourceMethod);
    }

    private void AddTypeReference(
        MethodDefinition sourceMethod,
        ITypeDefOrRef? type,
        ReferenceIndex referenceIndex
    )
    {
        switch (type)
        {
            case null:
                return;
            case TypeSpecification { Signature: { } signature }:
                AddTypeSignatureReferences(sourceMethod, signature, referenceIndex);
                return;
        }

        TypeDefinition? typeDefinition;
        try
        {
            typeDefinition = type.Resolve(dataProvider.Context);
        }
        catch
        {
            return;
        }

        if (typeDefinition is null || !typeDefinition.IsCompilerGenerated() || !IsCompilerGeneratedTypeCandidate(typeDefinition))
        {
            return;
        }

        AddReference(referenceIndex.TypeReferences, typeDefinition, sourceMethod);
    }

    private void AddFieldSignatureReferences(
        MethodDefinition sourceMethod,
        FieldSignature? signature,
        ReferenceIndex referenceIndex
    )
    {
        AddTypeSignatureReferences(sourceMethod, signature?.FieldType, referenceIndex);
    }

    private void AddMethodSignatureReferences(
        MethodDefinition sourceMethod,
        MethodSignatureBase? signature,
        ReferenceIndex referenceIndex
    )
    {
        if (signature is null)
        {
            return;
        }

        AddTypeSignatureReferences(sourceMethod, signature.ReturnType, referenceIndex);
        foreach (var parameterType in signature.ParameterTypes)
        {
            AddTypeSignatureReferences(sourceMethod, parameterType, referenceIndex);
        }

        if (signature is not MethodSignature methodSignature)
        {
            return;
        }

        foreach (var sentinelParameterType in methodSignature.SentinelParameterTypes)
        {
            AddTypeSignatureReferences(sourceMethod, sentinelParameterType, referenceIndex);
        }
    }

    private void AddTypeSignatureReferences(
        MethodDefinition sourceMethod,
        TypeSignature? signature,
        ReferenceIndex referenceIndex
    )
    {
        AddTypeSignatureReferences(
            sourceMethod,
            signature,
            referenceIndex,
            new HashSet<TypeSignature>(ReferenceEqualityComparer.Instance)
        );
    }

    private void AddTypeSignatureReferences(
        MethodDefinition sourceMethod,
        TypeSignature? signature,
        ReferenceIndex referenceIndex,
        ISet<TypeSignature> visited
    )
    {
        if (signature is null || !visited.Add(signature))
        {
            return;
        }

        switch (signature)
        {
            case TypeDefOrRefSignature typeSig:
                AddTypeReference(sourceMethod, typeSig.Type, referenceIndex);
                return;
            case GenericInstanceTypeSignature genericSig:
                AddTypeReference(sourceMethod, genericSig.GenericType, referenceIndex);
                foreach (var typeArg in genericSig.TypeArguments)
                {
                    AddTypeSignatureReferences(sourceMethod, typeArg, referenceIndex, visited);
                }
                return;
            case CustomModifierTypeSignature modifierSig:
                AddTypeReference(sourceMethod, modifierSig.ModifierType, referenceIndex);
                AddTypeSignatureReferences(sourceMethod, modifierSig.BaseType, referenceIndex, visited);
                return;
            case TypeSpecificationSignature specificationSig:
                AddTypeSignatureReferences(sourceMethod, specificationSig.BaseType, referenceIndex, visited);
                return;
            case FunctionPointerTypeSignature { Signature: { } functionSig }:
                AddFunctionPointerSignatureReferences(sourceMethod, functionSig, referenceIndex, visited);
                return;
        }
    }

    private void AddFunctionPointerSignatureReferences(
        MethodDefinition sourceMethod,
        MethodSignatureBase signature,
        ReferenceIndex referenceIndex,
        ISet<TypeSignature> visited
    )
    {
        AddTypeSignatureReferences(sourceMethod, signature.ReturnType, referenceIndex, visited);
        foreach (var parameterType in signature.ParameterTypes)
        {
            AddTypeSignatureReferences(sourceMethod, parameterType, referenceIndex, visited);
        }

        if (signature is not MethodSignature methodSignature)
        {
            return;
        }

        foreach (var sentinelParameterType in methodSignature.SentinelParameterTypes)
        {
            AddTypeSignatureReferences(sourceMethod, sentinelParameterType, referenceIndex, visited);
        }
    }

    private static void AddReference<TKey>(
        IDictionary<TKey, HashSet<MethodDefinition>> references,
        TKey generatedMember,
        MethodDefinition sourceMethod
    )
        where TKey : notnull
    {
        if (!references.TryGetValue(generatedMember, out var sourceMethods))
        {
            sourceMethods = [];
            references.Add(generatedMember, sourceMethods);
        }

        sourceMethods.Add(sourceMethod);
    }

    private string? GetUniqueTypeName(TypeDefinition generatedType, string baseName)
    {
        var sanitizedBaseName = SanitizeIdentifier(baseName);
        var declaredGenericArity = generatedType.GenericParameters.Count
            - (generatedType.DeclaringType?.GenericParameters.Count ?? 0);
        var genericArity = declaredGenericArity > 0
            ? $"`{declaredGenericArity}"
            : string.Empty;

        return GetUniqueName(
            sanitizedBaseName,
            candidate => HasTypeNameCollision(generatedType, $"{candidate}{genericArity}"),
            genericArity
        );
    }

    private string? GetUniqueMethodName(MethodDefinition generatedMethod, string baseName)
    {
        var sanitizedBaseName = SanitizeIdentifier(baseName);

        return GetUniqueName(
            sanitizedBaseName,
            candidate => HasMethodNameCollision(generatedMethod, candidate),
            string.Empty
        );
    }

    private static string? GetUniqueName(
        string baseName,
        Func<string, bool> hasCollision,
        string suffix
    )
    {
        if (!hasCollision(baseName))
        {
            return $"{baseName}{suffix}";
        }

        for (var i = 1; i < 1000; i++)
        {
            var candidate = $"{baseName}{i}";
            if (!hasCollision(candidate))
            {
                return $"{candidate}{suffix}";
            }
        }

        return null;
    }

    private bool HasTypeNameCollision(TypeDefinition generatedType, string candidateName)
    {
        if (generatedType.DeclaringType is null)
        {
            return dataProvider.LoadedModule!.TopLevelTypes.Any(type =>
                type != generatedType
                && type.Namespace == generatedType.Namespace
                && type.Name?.ToString() == candidateName
            );
        }

        return GetDeclaringTypeCollisionScopes(generatedType.DeclaringType).Any(type =>
            type.NestedTypes.Any(nestedType => nestedType != generatedType && nestedType.Name?.ToString() == candidateName)
        );
    }

    private IEnumerable<TypeDefinition> GetDeclaringTypeCollisionScopes(TypeDefinition declaringType)
    {
        yield return declaringType;

        var current = declaringType.BaseType;
        var guard = 0;

        while (current is not null && guard++ < 64)
        {
            if (!current.TryResolve(dataProvider.Context, out var baseDef))
            {
                yield break;
            }

            yield return baseDef;
            current = baseDef.BaseType;
        }
    }

    private static bool HasMethodNameCollision(MethodDefinition generatedMethod, string candidateName)
    {
        return generatedMethod.DeclaringType?.Methods.Any(method =>
            method != generatedMethod && method.Name?.ToString() == candidateName
        ) ?? false;
    }

    private static string GetMethodNamePrefix(MethodDefinition method)
    {
        var name = method.Name?.ToString() ?? "Method";

        return name switch
        {
            ".ctor" => "Ctor",
            ".cctor" => "Cctor",
            _ => name,
        };
    }

    private static string SanitizeIdentifier(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length);

        foreach (var c in name)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        var sanitized = builder.ToString().Trim('_');

        return string.IsNullOrEmpty(sanitized) ? "Method" : sanitized;
    }

    private static string RemoveGenericArity(string name)
    {
        var tickIndex = name.IndexOf('`', StringComparison.Ordinal);

        return tickIndex < 0 ? name : name[..tickIndex];
    }

    private static bool IsReferenceSourceMethod(MethodDefinition method)
    {
        return method.CilMethodBody is not null
            && method.Name is not null
            && !method.Name.IsObfuscatedName()
            && !method.IsCompilerGenerated();
    }

    private static bool IsCompilerGeneratedMethodCandidate(MethodDefinition method)
    {
        return method.Name?.ToString() is { } name
            && name.IsObfuscatedName()
            && !method.IsConstructor
            && !method.IsGetMethod
            && !method.IsSetMethod
            && !method.IsAddMethod
            && !method.IsRemoveMethod
            && !method.IsFireMethod
            && (method.IsCompilerGenerated() || method.DeclaringType?.IsCompilerGenerated() == true);
    }

    private static bool IsCompilerGeneratedTypeCandidate(TypeDefinition type) =>
        type.Name?.ToString() is { } name
        && name.IsObfuscatedName()
        && (type.IsClass || type.IsStruct());

    private void UpdateMethodMemberReferences(MethodDefinition target, Utf8String newName)
    {
        List<MemberReference> cachedReferences;

        try
        {
            cachedReferences = memberReferenceCache.GetMethodReferences(target);
        }
        catch (KeyNotFoundException)
        {
            return;
        }

        foreach (var reference in cachedReferences)
        {
            reference.Name = newName;
        }
    }

    private sealed class ReferenceIndex
    {
        public Dictionary<TypeDefinition, HashSet<MethodDefinition>> TypeReferences { get; } = [];
        public Dictionary<MethodDefinition, HashSet<MethodDefinition>> MethodReferences { get; } = [];
    }
}
