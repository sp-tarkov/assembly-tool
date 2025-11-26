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
public class FieldRenamer(DataProvider dataProvider, Statistics stats, MemberReferenceCache memberReferenceCache)
    : IRenamer
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

                if (field.Signature?.FieldType.Name != newTypeName)
                {
                    continue;
                }

                var newFieldName = GetNewFieldNameFromTypeRename(field, newTypeName, fieldCount);

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

                UpdateFieldReferences(field, newFieldName);
                field.Name = newFieldName;
            }
        }
    }

    public void RenamePublicizedFields(List<FieldDefinition> fieldsToRename)
    {
        foreach (var field in fieldsToRename.Where(f => !IsSerializedField(f)))
        {
            var newName = CapitalizeFieldName(field);
            field.Name = newName;
            UpdateFieldReferences(field, newName);
        }
    }

    private Utf8String GetNewFieldNameFromTypeRename(FieldDefinition field, string newName, int fieldCount = 0)
    {
        var newFieldCount = fieldCount > 0 ? $"_{fieldCount}" : string.Empty;

        // Prefix backing fields with an underscore
        var firstChar = field.IsBackingField() ? $"_{newName[0]}" : $"{char.ToUpper(newName[0])}";

        stats.FieldRenamedCount++;
        return new Utf8String($"{firstChar}{newName[1..]}{newFieldCount}");
    }

    private static Utf8String CapitalizeFieldName(FieldDefinition field)
    {
        var strName = field.Name!.ToString();

        // Prefix backing fields with an underscore
        switch (field.IsBackingField())
        {
            // Already a backing field denoted by the compiler or already prefixed with an underscore
            case true when strName.StartsWith('<'):
            case true when strName.StartsWith('_'):
                return new Utf8String(strName);

            case true when !strName.StartsWith('_'):
                return new Utf8String($"_{strName}");
        }

        if (strName.StartsWith('_'))
        {
            strName = strName[1..];
        }

        if (!char.IsUpper(strName[0]))
        {
            strName = $"{char.ToUpper(strName[0])}{strName[1..]}";
        }

        return new Utf8String(strName);
    }

    private void UpdateFieldReferences(FieldDefinition field, Utf8String newName)
    {
        var references = memberReferenceCache.GetFieldReferences(field);

        foreach (var reference in references)
        {
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
