using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Extensions;
using Serilog;
using Serilog.Events;
using SPTarkov.DI.Annotations;
using FieldDefinition = AsmResolver.DotNet.FieldDefinition;
using MethodDefinition = AsmResolver.DotNet.MethodDefinition;

namespace AssemblyLib.DirectMapper;

[Injectable]
public sealed class Renamer(Statistics stats)
{
    public void RenameObfuscatedFields(ModuleDefinition module, Utf8String oldTypeName, Utf8String newTypeName)
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

    public void RenameObfuscatedProperties(ModuleDefinition module, Utf8String oldTypeName, Utf8String newTypeName)
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
