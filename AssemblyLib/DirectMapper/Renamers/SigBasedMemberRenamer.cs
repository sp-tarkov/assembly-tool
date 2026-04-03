using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AssemblyLib.DirectMapper.SignatureComparers;
using AssemblyLib.Extensions;
using AssemblyLib.Shared;
using Serilog;
using Serilog.Events;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Renamers;

[Injectable]
public class SigBasedMemberRenamer(
    DataProvider dataProvider,
    MethodSigComparer methodSignatureComparer,
    PropertySigComparer propertySigComparer,
    MemberReferenceCache memberReferenceCache
)
{
    // Key - Target :: Val - Dummy
    private readonly Dictionary<TypeDefinition, TypeDefinition> _targetToDummyMap = [];

    public void RenameMembersBySignature()
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
        RenameAllTypes();
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
                Log.Error("Could not find dummy type {dummy} when building target to dummy map", target.FullName);
                continue;
            }

            _targetToDummyMap.Add(target, dummyType);
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Loaded {count} dummy types for member comparison", _targetToDummyMap.Count);
        }
    }

    private void RenameAllTypes()
    {
        foreach (var (targetType, dummyType) in _targetToDummyMap)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Renaming members on: {type}", targetType.FullName);
            }

            RenameMethodsOnType(targetType, dummyType);
            RenamePropertiesOnType(targetType, dummyType);
        }
    }

    private void RenameMethodsOnType(TypeDefinition targetType, TypeDefinition dummyType)
    {
        var targetMethods = targetType.Methods.Where(FilterMethods).Where(m => m.Name!.IsObfuscatedName());
        var dummyMethods = dummyType.Methods.Where(FilterMethods).ToList();

        var dummyMethodNames = dummyMethods.Select(m => m.Name).ToHashSet();

        foreach (var targetMethod in targetMethods)
        {
            // Already a named method, or is a void type method with no parameters
            if (
                dummyMethodNames.Contains(targetMethod.Name)
                || methodSignatureComparer.IsVoidMethodWithNoParameters(targetMethod)
            )
            {
                continue;
            }

            // Skip anything here that is virtual and isn't the new slot
            // virtual methods are handled separate
            if (targetMethod.IsVirtual && !targetMethod.IsNewSlot)
            {
                continue;
            }

            foreach (var dummyMethod in dummyMethods.ToArray())
            {
                if (
                    !methodSignatureComparer.IsSame(targetMethod, dummyMethod)
                    || targetMethod.Name!.Equals(dummyMethod.Name)
                )
                {
                    continue;
                }

                if (targetMethod.IsNewSlot)
                {
                    // TODO: Skip renaming virtual method chains on generic types for now, because its fucking hard.
                    if (targetMethod.DeclaringType!.HasGenericParameters)
                    {
                        break;
                    }

                    RenameVirtualMethodChain(targetMethod, dummyMethod);
                    dummyMethods.Remove(dummyMethod);
                    break;
                }

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Renaming method: {old} -> {new}", targetMethod.FullName, dummyMethod.FullName);
                }

                targetMethod.Name = dummyMethod.Name;
                UpdateMethodMemberReferences(targetMethod, targetMethod.Name!);

                dummyMethods.Remove(dummyMethod);
                break;
            }
        }
    }

    /// <summary>
    ///     Renames all methods in a virtual method chain
    /// </summary>
    /// <param name="targetMethod">base `newslot` method</param>
    /// <param name="dummyMethod">matching dummy method</param>
    private void RenameVirtualMethodChain(MethodDefinition targetMethod, MethodDefinition dummyMethod)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug(
                "Renaming base virtual method: {type1}::{old} -> {type2}::{new}",
                targetMethod.DeclaringType?.Name?.ToString(),
                targetMethod.Name?.ToString(),
                dummyMethod.DeclaringType?.Name?.ToString(),
                dummyMethod.Name?.ToString()
            );
        }

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

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "Renaming override method: {type3}::{old} -> {type4}::{new}",
                    type.Name?.ToString(),
                    impl.Name?.ToString(),
                    type.Name?.ToString(),
                    dummyMethod.Name?.ToString()
                );
            }

            impl.Name = dummyMethod.Name;
            UpdateMethodMemberReferences(impl, impl.Name!);
        }

        targetMethod.Name = dummyMethod.Name;
        UpdateMethodMemberReferences(targetMethod, targetMethod.Name!);
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

    private void RenamePropertiesOnType(TypeDefinition targetType, TypeDefinition dummyType)
    {
        var targetProperties = targetType.Properties;
        var dummyProperties = dummyType.Properties.ToList();

        // Removes properties that already exist
        dummyProperties.RemoveAll(f => targetProperties.Any(t => t.Name == f.Name));

        var dummyPropertiesNames = dummyProperties.Select(p => p.Name).ToHashSet();

        foreach (var targetProperty in targetProperties)
        {
            if (dummyPropertiesNames.Contains(targetProperty.Name))
            {
                continue;
            }

            foreach (var dummyProperty in dummyProperties.ToArray())
            {
                if (!propertySigComparer.IsSame(targetProperty, dummyProperty))
                {
                    continue;
                }

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Renaming property: {old} -> {new}", targetProperty.FullName, dummyProperty.FullName);
                }

                targetProperty.Name = dummyProperty.Name;
                dummyProperties.Remove(dummyProperty);
                break;
            }
        }
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

    private void UpdateMethodMemberReferences(MethodDefinition target, Utf8String newName)
    {
        var cachedReferences = memberReferenceCache.GetMethodReferences(target);

        foreach (var reference in cachedReferences)
        {
            reference.Name = newName;
        }
    }
}
