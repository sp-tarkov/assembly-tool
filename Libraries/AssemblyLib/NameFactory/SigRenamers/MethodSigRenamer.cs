using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AssemblyLib.Helpers;
using AssemblyLib.SignatureComparers;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory.SigRenamers;

[Injectable]
public class MethodSigRenamer(
    ILogger<MethodSigRenamer> logger,
    DataProvider dataProvider,
    MethodSigComparer methodSignatureComparer,
    MemberReferenceCache memberReferenceCache
) : ISigRenamer
{
    public int Priority => 0;
    public bool Enabled => true;
    public ERenamerType Type => ERenamerType.Methods;

    public void Rename(TypeDefinition targetType, TypeDefinition dummyType)
    {
        var consumedDummyMethods = new HashSet<MethodDefinition>();

        RenameUniqueSignatureMatches(targetType, dummyType, consumedDummyMethods);

        while (RenameAnchoredMatches(targetType, dummyType, consumedDummyMethods))
        {
            // Each rename becomes a new neighboring anchor for the next pass.
        }

        RenameUnanchoredOrderedFallbackMatches(targetType, dummyType, consumedDummyMethods);
    }

    private void RenameUniqueSignatureMatches(
        TypeDefinition targetType,
        TypeDefinition dummyType,
        ISet<MethodDefinition> consumedDummyMethods
    )
    {
        foreach (var targetMethod in GetTargetCandidates(targetType))
        {
            var dummyMatches = GetDummyCandidates(targetType, dummyType, consumedDummyMethods)
                .Where(dummyMethod => IsExactSignatureMatch(targetMethod, dummyMethod))
                .ToList();

            if (dummyMatches.Count != 1)
            {
                continue;
            }

            var dummyMethod = dummyMatches[0];
            var targetMatches = GetTargetCandidates(targetType)
                .Where(method => IsExactSignatureMatch(method, dummyMethod))
                .Take(2)
                .ToList();

            if (targetMatches.Count != 1)
            {
                continue;
            }

            if (ApplyRename(targetType, targetMethod, dummyMethod))
            {
                consumedDummyMethods.Add(dummyMethod);
            }
        }
    }

    private bool RenameAnchoredMatches(
        TypeDefinition targetType,
        TypeDefinition dummyType,
        ISet<MethodDefinition> consumedDummyMethods
    )
    {
        var targetMethods = GetTargetCandidates(targetType).ToList();
        var dummyMethods = GetDummyCandidates(targetType, dummyType, consumedDummyMethods).ToList();

        if (targetMethods.Count == 0 || dummyMethods.Count == 0)
        {
            return false;
        }

        var renamedAny = false;
        var targetGroups = targetMethods.GroupBy(GetShapeKey);
        var dummyGroups = dummyMethods.GroupBy(GetShapeKey).ToDictionary(group => group.Key);

        foreach (var targetGroup in targetGroups)
        {
            if (!dummyGroups.TryGetValue(targetGroup.Key, out var dummyGroup))
            {
                LogSkippedShapeGroup(
                    targetType,
                    targetGroup.Key,
                    targetGroup.Count(),
                    0,
                    "no matching dummy bucket"
                );
                continue;
            }

            var targetGroupMethods = targetGroup.ToList();
            var dummyGroupMethods = dummyGroup.ToList();

            if (targetGroupMethods.Count == 1 && dummyGroupMethods.Count == 1)
            {
                if (ApplyRename(targetType, targetGroupMethods[0], dummyGroupMethods[0]))
                {
                    consumedDummyMethods.Add(dummyGroupMethods[0]);
                    renamedAny = true;
                }

                continue;
            }

            if (targetGroupMethods.Count != dummyGroupMethods.Count)
            {
                LogSkippedShapeGroup(
                    targetType,
                    targetGroup.Key,
                    targetGroupMethods.Count,
                    dummyGroupMethods.Count,
                    "bucket counts differ; using unique anchors only"
                );
            }

            renamedAny |= RenameAnchoredGroup(
                targetType,
                dummyType,
                targetGroupMethods,
                dummyGroupMethods,
                consumedDummyMethods
            );
        }

        return renamedAny;
    }

    private bool RenameAnchoredGroup(
        TypeDefinition targetType,
        TypeDefinition dummyType,
        IReadOnlyList<MethodDefinition> targetMethods,
        IReadOnlyList<MethodDefinition> dummyMethods,
        ISet<MethodDefinition> consumedDummyMethods
    )
    {
        var renamedAny = false;
        var usedTargetMethods = new HashSet<MethodDefinition>();
        var usedDummyMethods = new HashSet<MethodDefinition>();

        renamedAny |= RenameAnchoredGroupBy(
            targetType,
            dummyType,
            targetMethods,
            dummyMethods,
            consumedDummyMethods,
            usedTargetMethods,
            usedDummyMethods,
            static (targetKey, dummyKey) =>
                targetKey.PreviousAnchor is not null
                && targetKey.NextAnchor is not null
                && targetKey.PreviousAnchor == dummyKey.PreviousAnchor
                && targetKey.NextAnchor == dummyKey.NextAnchor
        );

        renamedAny |= RenameAnchoredGroupBy(
            targetType,
            dummyType,
            targetMethods,
            dummyMethods,
            consumedDummyMethods,
            usedTargetMethods,
            usedDummyMethods,
            static (targetKey, dummyKey) =>
                targetKey.PreviousAnchor is not null
                && targetKey.PreviousAnchor == dummyKey.PreviousAnchor
        );

        renamedAny |= RenameAnchoredGroupBy(
            targetType,
            dummyType,
            targetMethods,
            dummyMethods,
            consumedDummyMethods,
            usedTargetMethods,
            usedDummyMethods,
            static (targetKey, dummyKey) =>
                targetKey.NextAnchor is not null
                && targetKey.NextAnchor == dummyKey.NextAnchor
        );

        return renamedAny;
    }

    private bool RenameAnchoredGroupBy(
        TypeDefinition targetType,
        TypeDefinition dummyType,
        IReadOnlyList<MethodDefinition> targetMethods,
        IReadOnlyList<MethodDefinition> dummyMethods,
        ISet<MethodDefinition> consumedDummyMethods,
        ISet<MethodDefinition> usedTargetMethods,
        ISet<MethodDefinition> usedDummyMethods,
        Func<MatchKey, MatchKey, bool> isMatch
    )
    {
        var renamedAny = false;

        foreach (var targetMethod in targetMethods.Where(method => !usedTargetMethods.Contains(method)))
        {
            var targetKey = GetMatchKey(targetType, targetMethod);
            var dummyMatches = dummyMethods
                .Where(method => !usedDummyMethods.Contains(method))
                .Where(method => isMatch(targetKey, GetMatchKey(dummyType, method)))
                .Take(2)
                .ToList();

            if (dummyMatches.Count != 1)
            {
                continue;
            }

            var dummyMethod = dummyMatches[0];
            var dummyKey = GetMatchKey(dummyType, dummyMethod);
            var targetMatches = targetMethods
                .Where(method => !usedTargetMethods.Contains(method))
                .Where(method => isMatch(GetMatchKey(targetType, method), dummyKey))
                .Take(2)
                .ToList();

            if (targetMatches.Count != 1)
            {
                continue;
            }

            if (!ApplyRename(targetType, targetMethod, dummyMethod))
            {
                continue;
            }

            consumedDummyMethods.Add(dummyMethod);
            usedTargetMethods.Add(targetMethod);
            usedDummyMethods.Add(dummyMethod);
            renamedAny = true;
        }

        return renamedAny;
    }

    private void RenameUnanchoredOrderedFallbackMatches(
        TypeDefinition targetType,
        TypeDefinition dummyType,
        ISet<MethodDefinition> consumedDummyMethods
    )
    {
        var targetGroups = GetTargetCandidates(targetType).GroupBy(GetShapeKey);
        var dummyGroups = GetDummyCandidates(targetType, dummyType, consumedDummyMethods)
            .GroupBy(GetShapeKey)
            .ToDictionary(group => group.Key);

        foreach (var targetGroup in targetGroups)
        {
            if (IsAmbiguousForOrderedFallback(targetGroup.Key))
            {
                LogSkippedFallbackGroup(
                    targetType,
                    targetGroup.Key,
                    targetGroup.Count(),
                    dummyGroups.TryGetValue(targetGroup.Key, out var ambiguousDummy)
                        ? ambiguousDummy.Count()
                        : 0,
                    "ambiguous shape (void, no parameters); ordered fallback disabled"
                );
                continue;
            }

            if (!dummyGroups.TryGetValue(targetGroup.Key, out var dummyGroup))
            {
                LogSkippedFallbackGroup(
                    targetType,
                    targetGroup.Key,
                    targetGroup.Count(),
                    0,
                    "no matching dummy bucket"
                );
                continue;
            }

            if (targetGroup.Count() != dummyGroup.Count())
            {
                LogSkippedFallbackGroup(
                    targetType,
                    targetGroup.Key,
                    targetGroup.Count(),
                    dummyGroup.Count(),
                    "bucket counts differ; applying ordered min-count fallback"
                );
            }

            var targetMethods = targetGroup.ToList();
            var dummyMethods = dummyGroup.ToList();
            var count = Math.Min(targetMethods.Count, dummyMethods.Count);

            for (var i = 0; i < count; i++)
            {
                if (ApplyRename(targetType, targetMethods[i], dummyMethods[i]))
                {
                    consumedDummyMethods.Add(dummyMethods[i]);
                }
            }
        }
    }

    private bool ApplyRename(TypeDefinition targetType, MethodDefinition targetMethod, MethodDefinition dummyMethod)
    {
        if (HasMethodCollision(targetType, targetMethod, dummyMethod))
        {
            logger.LogWarning(
                "Trying to set duplicate method name: {methodName} in class {className}. Skipping.",
                dummyMethod.Name?.ToString(),
                targetType.FullName
            );

            return false;
        }

        if (targetMethod.IsNewSlot)
        {
            // TODO: Skip renaming virtual method chains on generic types for now, because its fucking hard.
            if (targetMethod.DeclaringType!.HasGenericParameters)
            {
                return false;
            }

            return RenameVirtualMethodChain(targetMethod, dummyMethod);
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Renaming method: {old} -> {new}", targetMethod.FullName, dummyMethod.FullName);
        }

        targetMethod.Name = dummyMethod.Name;
        UpdateMethodMemberReferences(targetMethod, targetMethod.Name!);

        return true;
    }

    /// <summary>
    ///     Renames all methods in a virtual method chain
    /// </summary>
    /// <param name="targetMethod">base `newslot` method</param>
    /// <param name="dummyMethod">matching dummy method</param>
    private bool RenameVirtualMethodChain(MethodDefinition targetMethod, MethodDefinition dummyMethod)
    {
        var overrides = new List<MethodDefinition>();

        foreach (var type in dataProvider.LoadedModule!.GetAllTypes())
        {
            if (type.FullName == dummyMethod.DeclaringType?.FullName)
            {
                continue;
            }

            // TODO: This doesn't fucking handle generics, because why would it.
            // So now we have to go write some bullshit code to resolve all the places BSG loves
            // to use parametric polymorphism so we can rename those
            if (!type.InheritsFrom(targetMethod.DeclaringType?.FullName ?? string.Empty))
            {
                continue;
            }

            var impl = FindMethodImplementationInType(type, targetMethod);
            if (impl == null)
            {
                continue;
            }

            if (HasMethodCollision(type, impl, dummyMethod))
            {
                logger.LogWarning(
                    "Virtual chain rename aborted: '{newName}' collides on derived type '{type}'. "
                        + "Base method '{base}' left unchanged to preserve vtable.",
                    dummyMethod.Name?.ToString(),
                    type.FullName,
                    targetMethod.FullName
                );

                return false;
            }

            overrides.Add(impl);
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Renaming base virtual method: {type1}::{old} -> {type2}::{new}",
                targetMethod.DeclaringType?.Name?.ToString(),
                targetMethod.Name?.ToString(),
                dummyMethod.DeclaringType?.Name?.ToString(),
                dummyMethod.Name?.ToString()
            );
        }

        foreach (var impl in overrides)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Renaming override method: {type3}::{old} -> {type4}::{new}",
                    impl.DeclaringType?.Name?.ToString(),
                    impl.Name?.ToString(),
                    impl.DeclaringType?.Name?.ToString(),
                    dummyMethod.Name?.ToString()
                );
            }

            impl.Name = dummyMethod.Name;
            UpdateMethodMemberReferences(impl, impl.Name!);
        }

        targetMethod.Name = dummyMethod.Name;
        UpdateMethodMemberReferences(targetMethod, targetMethod.Name!);
        return true;
    }

    /// <summary>
    ///     Finds method implementations for a virtual base method in the provided type
    /// </summary>
    /// <param name="type">type to search</param>
    /// <param name="baseMethod">base method to search for an impl of</param>
    /// <returns>def of impl or null</returns>
    private static MethodDefinition? FindMethodImplementationInType(TypeDefinition type, MethodDefinition baseMethod)
    {
        foreach (var method in type.Methods.Where(m => m.IsVirtual && m.IsReuseSlot))
        {
            if (
                SignatureComparer.Default.Equals(method.Signature, baseMethod.Signature)
                && method.Name == baseMethod.Name
            )
            {
                return method;
            }
        }

        return null;
    }

    private MatchKey GetMatchKey(TypeDefinition type, MethodDefinition method)
    {
        var index = type.Methods.IndexOf(method);

        return new MatchKey(GetShapeKey(method), FindPreviousAnchor(type, index), FindNextAnchor(type, index));
    }

    private static MethodAnchor? FindPreviousAnchor(TypeDefinition type, int methodIndex)
    {
        for (var i = methodIndex - 1; i >= 0; i--)
        {
            if (TryGetAnchor(type.Methods[i], out var anchor))
            {
                return anchor;
            }
        }

        return null;
    }

    private static MethodAnchor? FindNextAnchor(TypeDefinition type, int methodIndex)
    {
        for (var i = methodIndex + 1; i < type.Methods.Count; i++)
        {
            if (TryGetAnchor(type.Methods[i], out var anchor))
            {
                return anchor;
            }
        }

        return null;
    }

    private static bool TryGetAnchor(MethodDefinition method, out MethodAnchor? anchor)
    {
        anchor = null;

        if (!FilterMethods(method) || method.Name is null || method.Name.IsObfuscatedName())
        {
            return false;
        }

        anchor = new MethodAnchor(method.Name.ToString(), GetShapeKey(method));
        return true;
    }

    private static MethodShapeKey GetShapeKey(MethodDefinition method)
    {
        return new MethodShapeKey(
            method.Signature?.HasThis ?? false,
            method.Signature?.ReturnsValue ?? false,
            method.Signature?.ReturnType.FullName ?? string.Empty,
            method.Signature?.GetTotalParameterCount() ?? 0,
            method.Signature?.SentinelParameterTypes.Count ?? 0,
            method.Signature?.GenericParameterCount ?? 0,
            method.GenericParameters.Count,
            GetParamTypeKey(method)
        );
    }

    private static string GetParamTypeKey(MethodDefinition method)
    {
        return method.Signature is null
            ? string.Empty
            : string.Join(",", method.Signature.ParameterTypes.Select(p => p.FullName));
    }

    private static MethodCollisionKey GetCollisionKey(MethodDefinition method)
    {
        return new MethodCollisionKey(GetParamTypeKey(method), method.GenericParameters.Count);
    }

    private static bool HasMethodCollision(
        TypeDefinition targetType,
        MethodDefinition targetMethod,
        MethodDefinition dummyMethod
    )
    {
        var newName = dummyMethod.Name;
        var collisionKey = GetCollisionKey(dummyMethod);

        return targetType.Methods.Any(method =>
            method != targetMethod && method.Name == newName && GetCollisionKey(method) == collisionKey
        );
    }

    private static bool HasExistingNamedMethodForDummy(TypeDefinition targetType, MethodDefinition dummyMethod)
    {
        var dummyName = dummyMethod.Name;
        var collisionKey = GetCollisionKey(dummyMethod);

        return targetType.Methods.Any(method =>
            method.Name == dummyName && !method.Name!.IsObfuscatedName() && GetCollisionKey(method) == collisionKey
        );
    }

    private bool IsExactSignatureMatch(MethodDefinition targetMethod, MethodDefinition dummyMethod)
    {
        return GetShapeKey(targetMethod) == GetShapeKey(dummyMethod)
            && methodSignatureComparer.IsSame(targetMethod, dummyMethod);
    }

    private static IEnumerable<MethodDefinition> GetTargetCandidates(TypeDefinition targetType)
    {
        return targetType.Methods.Where(method =>
            FilterMethods(method)
            && method.Name is not null
            && method.Name.IsObfuscatedName()
            && (!method.IsVirtual || method.IsNewSlot)
        );
    }

    private static IEnumerable<MethodDefinition> GetDummyCandidates(
        TypeDefinition targetType,
        TypeDefinition dummyType,
        ISet<MethodDefinition> consumedDummyMethods
    )
    {
        return dummyType.Methods.Where(method =>
            FilterMethods(method)
            && method.Name is not null
            && !method.Name.IsObfuscatedName()
            && !consumedDummyMethods.Contains(method)
            && !HasExistingNamedMethodForDummy(targetType, method)
        );
    }

    /// <summary>
    /// Parameterless void methods carry no signature information; ordered min-count fallback
    /// can mislabel one as a Unity lifecycle hook (OnDestroy, Awake, ...) and break runtime
    /// dispatch. They're still allowed through the unique and anchored passes, which require
    /// stronger evidence than position alone.
    /// </summary>
    private static bool IsAmbiguousForOrderedFallback(MethodShapeKey shape)
    {
        return shape.ReturnType is "System.Void" or ""
            && shape.ParameterTypes.Length == 0;
    }

    private void UpdateMethodMemberReferences(MethodDefinition target, Utf8String newName)
    {
        var cachedReferences = memberReferenceCache.GetMethodReferences(target);

        foreach (var reference in cachedReferences)
        {
            reference.Name = newName;
        }
    }

    private void LogSkippedShapeGroup(
        TypeDefinition targetType,
        MethodShapeKey key,
        int targetCount,
        int dummyCount,
        string reason
    )
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogDebug(
            "Skipping method shape bucket on {type}: target={targetCount}, dummy={dummyCount}, "
                + "reason={reason}, key={key}",
            targetType.FullName,
            targetCount,
            dummyCount,
            reason,
            key
        );
    }

    private void LogSkippedFallbackGroup(
        TypeDefinition targetType,
        MethodShapeKey key,
        int targetCount,
        int dummyCount,
        string reason
    )
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogDebug(
            "Skipping method fallback bucket on {type}: target={targetCount}, dummy={dummyCount}, "
                + "reason={reason}, key={key}",
            targetType.FullName,
            targetCount,
            dummyCount,
            reason,
            key
        );
    }

    private static bool FilterMethods(MethodDefinition m)
    {
        return !m.IsCompilerControlled
            && !m.IsCompilerGenerated()
            && !m.IsGetMethod
            && !m.IsSetMethod
            && !m.IsConstructor
            && !m.IsAddMethod
            && !m.IsRemoveMethod
            && !m.IsFireMethod;
    }

    private readonly record struct MatchKey(
        MethodShapeKey Shape,
        MethodAnchor? PreviousAnchor,
        MethodAnchor? NextAnchor
    );

    private readonly record struct MethodAnchor(string Name, MethodShapeKey Shape);

    private readonly record struct MethodShapeKey(
        bool HasThis,
        bool ReturnsValue,
        string ReturnType,
        int TotalParameterCount,
        int SentinelParameterCount,
        int SignatureGenericParameterCount,
        int GenericParameterCount,
        string ParameterTypes
    );

    private readonly record struct MethodCollisionKey(string ParameterTypes, int GenericParameterCount);
}
