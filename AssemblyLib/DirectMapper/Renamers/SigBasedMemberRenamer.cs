using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Serialized;
using AssemblyLib.DirectMapper.SignatureComparers;
using AssemblyLib.Extensions;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Renamers;

[Injectable]
public class SigBasedMemberRenamer(
    DataProvider dataProvider,
    MethodSigComparer methodSignatureComparer,
    FieldSigComparer fieldSignatureComparer,
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

        Log.Information("Loaded {count} dummy types for member comparison", _targetToDummyMap.Count);
    }

    private void RenameAllTypes()
    {
        foreach (var (targetType, dummyType) in _targetToDummyMap)
        {
            Log.Information("Renaming members on: {type}", targetType.FullName);

            //RenameMethodsOnType(targetType, dummyType);
            //RenameFieldsOnType(targetType, dummyType);
            RenamePropertiesOnType(targetType, dummyType);
        }
    }

    private void RenameMethodsOnType(TypeDefinition targetType, TypeDefinition dummyType)
    {
        var targetMethods = targetType.Methods.Where(FilterMethods);
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

            foreach (var dummyMethod in dummyMethods.ToArray())
            {
                if (!methodSignatureComparer.IsSame(targetMethod, dummyMethod))
                {
                    continue;
                }

                Log.Information("Renaming method: {old} -> {new}", targetMethod.FullName, dummyMethod.FullName);
                targetMethod.Name = dummyMethod.Name;
                UpdateMethodMemberReferences(targetMethod, targetMethod.Name!);

                var overrides = memberReferenceCache.GetMethodOverrides(targetMethod);
                if (overrides.Count != 0)
                {
                    foreach (var method in overrides)
                    {
                        method.Name = dummyMethod.Name;
                        UpdateMethodMemberReferences(method, method.Name!);
                    }
                }

                dummyMethods.Remove(dummyMethod);
                break;
            }
        }
    }

    private void RenameFieldsOnType(TypeDefinition targetType, TypeDefinition dummyType)
    {
        var targetFields = targetType.Fields.Where(FilterFields);
        var dummyFields = dummyType.Fields.Where(FilterFields).ToList();

        // Removes fields that already exist
        dummyFields.RemoveAll(f => targetFields.Any(t => t.Name == f.Name));

        var dummyFieldNames = dummyFields.Select(f => f.Name).ToHashSet();

        foreach (var targetField in targetFields)
        {
            if (dummyFieldNames.Contains(targetField.Name))
            {
                continue;
            }

            foreach (var dummyField in dummyFields.ToArray())
            {
                if (!fieldSignatureComparer.IsSame(targetField, dummyField))
                {
                    continue;
                }

                if (
                    targetType.BaseType is TypeDefinition baseType
                    && baseType.Fields.Any(f => f.Name == dummyField.Name)
                )
                {
                    Log.Information(
                        "Ignoring rename of field as Super class has a field with the same name. Dummy: {dummy} -> Target: {target}",
                        dummyField.FullName,
                        targetField.FullName
                    );
                    continue;
                }

                Log.Information("Renaming field: {old} -> {new}", targetField.FullName, dummyField.FullName);

                targetField.Name = dummyField.Name;
                UpdateFieldMemberReferences(targetField, targetField.Name!);

                dummyFields.Remove(dummyField);
                break;
            }
        }
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

                Log.Information("Renaming property: {old} -> {new}", targetProperty.FullName, dummyProperty.FullName);

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

    private static bool FilterFields(FieldDefinition f)
    {
        return !f.IsCompilerGenerated();
    }

    private void UpdateFieldMemberReferences(FieldDefinition target, Utf8String newName)
    {
        var cachedReferences = memberReferenceCache.GetFieldReferences(target);

        foreach (var reference in cachedReferences)
        {
            reference.Name = newName;
        }
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
