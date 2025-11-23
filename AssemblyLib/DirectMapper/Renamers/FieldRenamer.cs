using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Extensions;
using AssemblyLib.Models;
using AssemblyLib.Shared;
using Serilog;
using Serilog.Events;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Renamers;

[Injectable]
public class FieldRenamer(DataProvider dataProvider, Statistics stats) : IRenamer
{
    public int Priority { get; } = 1;

    public ERenamerType Type
    {
        get { return ERenamerType.Fields; }
    }

    public void Rename(DirectMapModel model)
    {
        RenameObfuscatedFields(dataProvider.LoadedModule!, model.ToolData.ShortOldName!, model.NewName!);
    }

    private void RenameObfuscatedFields(ModuleDefinition module, Utf8String oldTypeName, Utf8String newTypeName)
    {
        foreach (var type in module.GetAllTypes())
        {
            var fields = type.Fields.Where(field => field.Name!.IsObfuscatedName());

            var fieldCount = 0;
            foreach (var field in fields)
            {
                if (IsSerializedField(field))
                {
                    continue;
                }

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

    private Utf8String GetNewFieldName(FieldDefinition field, string newName, int fieldCount = 0)
    {
        var newFieldCount = fieldCount > 0 ? $"_{fieldCount}" : string.Empty;

        var firstChar = field.IsPublic ? char.ToUpper(newName[0]) : char.ToLower(newName[0]);

        stats.FieldRenamedCount++;
        return new Utf8String($"{firstChar}{newName[1..]}{newFieldCount}");
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

    private static bool IsSerializedField(FieldDefinition field)
    {
        // DO NOT RENAME SERIALIZED FIELDS, IT BREAKS UNITY
        return field
            .CustomAttributes.Select(s => s.Constructor?.DeclaringType?.FullName)
            .Contains("UnityEngine.SerializeField");
    }
}
