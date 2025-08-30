using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Utils;
using Serilog;
using Serilog.Events;
using SPTarkov.DI.Annotations;
using FieldDefinition = AsmResolver.DotNet.FieldDefinition;
using MethodDefinition = AsmResolver.DotNet.MethodDefinition;

namespace AssemblyLib.ReMapper;

[Injectable]
public sealed class Renamer(Statistics stats)
{
    public void RenamePublicizedFieldAndUpdateMemberRefs(ModuleDefinition module, FieldDefinition fieldDef)
    {
        var origName = fieldDef.Name?.ToString();

        var newName = string.Empty;

        if (origName is null || origName.Length < 3 || IsSerializedField(fieldDef))
        {
            return;
        }

        // Handle underscores
        if (origName[0] == '_')
        {
            newName = char.ToUpper(origName[1]) + origName[2..];
        }

        if (char.IsLower(origName[0]))
        {
            newName = char.ToUpper(origName[0]) + origName[1..];
        }

        if (newName == string.Empty)
        {
            return;
        }

        var fields = fieldDef.DeclaringType?.Fields;
        var props = fieldDef.DeclaringType?.Properties;

        if (
            (fields is not null && fields.Any(f => f.Name == newName))
            || (props is not null && props.Any(p => p.Name == newName))
        )
        {
            newName += "_1";
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug(
                "Renaming Publicized field [{FieldDefDeclaringType}::{OrigName}] to [{TypeDefinition}::{NewName}]",
                fieldDef.DeclaringType,
                origName,
                fieldDef.DeclaringType,
                newName
            );
        }

        var newUtf8Name = new Utf8String(newName);

        UpdateMemberReferences(module, fieldDef, newUtf8Name);
        fieldDef.Name = newUtf8Name;
    }

    public void RenameRemap(ModuleDefinition module, RemapModel remap)
    {
        // Rename all fields and properties first

        // TODO: Passing strings as Utf8String, fix the model
        RenameObfuscatedFields(module, remap.ChosenType!.Name!, remap.NewTypeName);

        RenameObfuscatedProperties(module, remap.ChosenType!.Name!, remap.NewTypeName);

        remap.ChosenType.Name = new Utf8String(remap.NewTypeName);
    }

    public async Task FixInterfaceMangledMethodNames(ModuleDefinition module)
    {
        var types = module.GetAllTypes().Where(t => !t.IsInterface);

        var tasks = new List<Task>(types.Count());
        foreach (var type in types)
        {
            tasks.Add(
                Task.Factory.StartNew(() =>
                {
                    var renamedMethodNames = new List<Utf8String>();
                    foreach (var method in type.Methods)
                    {
                        if (method.IsConstructor || method.IsSetMethod || method.IsGetMethod)
                        {
                            continue;
                        }

                        var newMethodName = FixInterfaceMangledMethod(module, method, renamedMethodNames);

                        if (newMethodName == Utf8String.Empty)
                        {
                            continue;
                        }

                        renamedMethodNames.Add(newMethodName);
                    }
                })
            );
        }

        await Task.WhenAll(tasks);
    }

    private Utf8String FixInterfaceMangledMethod(
        ModuleDefinition module,
        MethodDefinition method,
        List<Utf8String> renamedMethodsOnType
    )
    {
        // Cache the method name early to avoid multiple accesses
        var methodName = method.Name?.ToString();
        var splitName = methodName?.Split('.');

        if (splitName is null || splitName.Length < 2)
        {
            return Utf8String.Empty;
        }

        var realMethodNameString = splitName.Last();
        var newName = new Utf8String(realMethodNameString);
        var sameNameCount = renamedMethodsOnType.Count(c => c == newName);

        if (sameNameCount > 0)
        {
            newName = new Utf8String($"{realMethodNameString}_{sameNameCount}");
        }

        // Cache existing method names to avoid concurrent enumeration
        var existingMethodNames = method
            .DeclaringType!.Methods.Select(m => m.Name?.ToString())
            .Where(name => name != null)
            .ToHashSet();

        if (existingMethodNames.Contains(realMethodNameString))
        {
            newName = new Utf8String($"{realMethodNameString}_1");
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug(
                "Renaming method [{MethodDeclaringType}::{MethodName}] to [{TypeDefinition}::{Utf8String}]",
                method.DeclaringType,
                methodName,
                method.DeclaringType,
                newName.ToString()
            );
        }

        UpdateMemberReferences(module, method, newName);
        method.Name = newName;

        return newName;
    }

    private void RenameObfuscatedFields(ModuleDefinition module, Utf8String oldTypeName, Utf8String newTypeName)
    {
        foreach (var type in module.GetAllTypes())
        {
            var fields = type.Fields.Where(field => field.Name!.IsObfuscatedName());

            var fieldCount = 0;
            foreach (var field in fields)
            {
                if (field.Signature?.FieldType.Name != oldTypeName)
                {
                    continue;
                }

                var newFieldName = GetNewFieldName(field, newTypeName, fieldCount);

                // Dont need to do extra work
                if (field.Name == newFieldName)
                {
                    continue;
                }

                var oldName = field.Name;

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug(
                        "Renaming field [{FieldDeclaringType}::{Utf8String}] to [{TypeDefinition}::{NewFieldName}]",
                        field.DeclaringType,
                        oldName?.ToString(),
                        field.DeclaringType,
                        newFieldName.ToString()
                    );
                }

                fieldCount++;

                UpdateMemberReferences(module, field, newFieldName);
                field.Name = newFieldName;
            }
        }
    }

    private void RenameObfuscatedProperties(ModuleDefinition module, Utf8String oldTypeName, Utf8String newTypeName)
    {
        foreach (var type in module.GetAllTypes())
        {
            var properties = type.Properties.Where(prop => prop.Name!.IsObfuscatedName());

            var propertyCount = 0;
            foreach (var property in properties)
            {
                if (property.Signature!.ReturnType.Name != oldTypeName)
                {
                    continue;
                }

                var newPropertyName = GetNewPropertyName(newTypeName, propertyCount);

                // Dont need to do extra work
                if (property.Name == newPropertyName)
                {
                    continue;
                }

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug(
                        "Renaming property [{PropertyDeclaringType}::{PropertyName}] to [{TypeDefinition}::{NewPropertyName}]",
                        property.DeclaringType,
                        property.Name?.ToString(),
                        property.DeclaringType,
                        newPropertyName.ToString()
                    );
                }

                property.Name = newPropertyName;

                propertyCount++;
            }
        }
    }

    private Utf8String GetNewFieldName(FieldDefinition field, string newName, int fieldCount = 0)
    {
        var newFieldCount = fieldCount > 0 ? $"_{fieldCount}" : string.Empty;

        var firstChar = field.IsPublic ? char.ToUpper(newName[0]) : char.ToLower(newName[0]);

        stats.FieldRenamedCount++;
        return new Utf8String($"{firstChar}{newName[1..]}{newFieldCount}");
    }

    private Utf8String GetNewPropertyName(string newName, int propertyCount = 0)
    {
        stats.PropertyRenamedCount++;

        return new Utf8String(propertyCount > 0 ? $"{newName}_{propertyCount}" : newName);
    }

    private static void UpdateMemberReferences(ModuleDefinition module, FieldDefinition target, Utf8String newName)
    {
        foreach (var reference in module.GetImportedMemberReferences())
        {
            if (reference.Resolve() != target)
            {
                continue;
            }

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "Updating Field Reference to [{TargetDeclaringType}::{TargetName}] to [{TypeDefinition}::{Utf8String}]",
                    target.DeclaringType,
                    target.Name?.ToString(),
                    target.DeclaringType,
                    newName.ToString()
                );
            }

            reference.Name = newName;
        }
    }

    private void UpdateMemberReferences(ModuleDefinition module, MethodDefinition target, Utf8String newName)
    {
        foreach (var reference in module.GetImportedMemberReferences())
        {
            if (reference.Resolve() != target)
            {
                continue;
            }

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "Updating Field Reference to [{TargetDeclaringType}::{TargetName}] to [{TypeDefinition}::{Utf8String}]",
                    target.DeclaringType,
                    target.Name?.ToString(),
                    target.DeclaringType,
                    newName.ToString()
                );
            }

            reference.Name = newName;
        }
    }

    private static bool IsSerializedField(FieldDefinition field)
    {
        // DO NOT RENAME SERIALIZED FIELDS, IT BREAKS UNITY
        return field
            .CustomAttributes.Select(s => s.Constructor?.DeclaringType?.FullName)
            .Contains("UnityEngine.SerializeField");
    }
}
