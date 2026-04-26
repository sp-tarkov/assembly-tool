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
    public int Priority { get; } = 0;
    public bool Enabled { get; } = false;

    public ERenamerType Type
    {
        get { return ERenamerType.Fields; }
    }

    public void Rename(DirectMapModel model)
    {
        var toolData = model.ToolData;

        var fieldsToRename = model.FieldRenames;
        if (fieldsToRename is null || fieldsToRename.Count == 0)
        {
            return;
        }

        foreach (var field in toolData.Type!.Fields)
        {
            if (fieldsToRename.TryGetValue(field.Name!.ToString(), out var newName))
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("\t\tField: {old} -> {new}", field.Name.ToString(), newName);
                }

                field.Name = new Utf8String(newName);
                UpdateFieldReferences(field, field.Name);
            }
        }
    }

    public void RenameObfuscatedFields()
    {
        foreach (var type in dataProvider.LoadedModule!.GetAllTypes())
        {
            if (type.IsEnum)
            {
                continue;
            }

            // We only want fields that have obfuscated names where their declaring type isn't obfuscated
            var fields = type.Fields.Where(field =>
                field.Name!.IsObfuscatedName() && !(field.Signature?.FieldType.Name?.IsObfuscatedName() ?? true)
            );

            foreach (var field in fields)
            {
                // Skip these dirty serialized bastards, this will 100% break the game, bad.
                if (IsSerializedField(field))
                {
                    continue;
                }

                if (field.Signature?.FieldType.Name is null)
                {
                    Log.Warning(
                        "Found a null field signature: {dclName}::{fName} when renaming obfuscated fields. Skipping.",
                        field.DeclaringType?.Name?.ToString(),
                        field.Name?.ToString()
                    );

                    continue;
                }

                var newFieldName = GetNewFieldNameFromTypeRename(field, field.Signature.FieldType.Name);

                if (field.DeclaringType?.Fields.Any(f => f.Name == newFieldName) ?? false)
                {
                    Log.Warning(
                        "Trying to set duplicate field name: {fName} in class {cName}. Skipping.",
                        newFieldName.ToString(),
                        field.DeclaringType.Name?.ToString()
                    );

                    continue;
                }

                // Dont need to do extra work
                if (field.Name == newFieldName)
                {
                    continue;
                }

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug(
                        "Renaming field [{FieldDeclaringType}::{Utf8String}] to [{TypeDefinition}::{NewFieldName}]",
                        field.DeclaringType,
                        field.Name?.ToString(),
                        field.DeclaringType,
                        newFieldName.ToString()
                    );
                }

                UpdateFieldReferences(field, newFieldName);
                field.Name = newFieldName;
            }
        }
    }

    private Utf8String GetNewFieldNameFromTypeRename(FieldDefinition field, string newName)
    {
        var genericSplit = newName.Split('`');
        if (genericSplit.Length > 1)
        {
            newName = genericSplit[0];
        }

        if (field.IsPrivate)
        {
            newName = $"{char.ToLower(newName[0])}{newName[1..]}";
        }

        var first = newName[0].ToString();
        if (field.IsBackingField())
        {
            // Remove 'i' from interfaces that are backing fields
            if (
                newName.StartsWith("i", StringComparison.CurrentCultureIgnoreCase)
                && (field.Signature?.FieldType.TryResolve(dataProvider.Context, out var typeDef) ?? false)
                && typeDef.IsInterface
            )
            {
                newName = newName[1..];
            }

            // ToLower() again in the rare event this might be a public backing field? -- 10 mins later, yup they exist.
            first = $"_{char.ToLower(newName[0])}";
        }

        var fieldCount =
            field.DeclaringType?.Fields.Count(f =>
                f.Name!.StartsWith(newName, StringComparison.CurrentCultureIgnoreCase)
                || f.Name!.StartsWith($"_{newName}", StringComparison.CurrentCultureIgnoreCase)
            ) ?? 0;

        var countPostfix = fieldCount > 0 ? $"_{fieldCount}" : string.Empty;

        stats.FieldRenamedCount++;
        return new Utf8String($"{first}{newName[1..]}{countPostfix}");
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
