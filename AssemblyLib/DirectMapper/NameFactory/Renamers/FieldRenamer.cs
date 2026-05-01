using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Extensions;
using AssemblyLib.Helpers;
using AssemblyLib.Models;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.NameFactory.Renamers;

[Injectable]
public class FieldRenamer(
    ILogger<FieldRenamer> logger,
    DataProvider dataProvider,
    Statistics stats,
    MemberReferenceCache memberReferenceCache
) : IRenamer
{
    public int Priority => 0;
    public bool Enabled => false;

    public ERenamerType Type => ERenamerType.Fields;

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
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("\t\tField: {old} -> {new}", field.Name.ToString(), newName);
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

            // Skip these dirty serialized bastards, this will 100% break the game, bad.
            foreach (var field in fields.Where(f => !f.IsUnitySerializedField()))
            {
                if (field.Signature?.FieldType.Name is null)
                {
                    logger.LogWarning(
                        "Found a null field signature: {dclName}::{fName} when renaming obfuscated fields. Skipping.",
                        field.DeclaringType?.Name?.ToString(),
                        field.Name?.ToString()
                    );

                    continue;
                }

                var newFieldName = GetNewFieldNameFromTypeRename(field, field.Signature.FieldType.Name);

                if (field.DeclaringType?.Fields.Any(f => f.Name == newFieldName) ?? false)
                {
                    logger.LogWarning(
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

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
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

    public void FixCapitalizationOnPublicizedFields()
    {
        foreach (var type in dataProvider.LoadedModule!.GetAllTypes())
        {
            if (type.IsEnum)
            {
                continue;
            }

            foreach (var field in type.Fields.Where(f => !f.IsUnitySerializedField()))
            {
                var newName = FieldNameToUpper(field);
                if (newName is null)
                {
                    continue;
                }

                UpdateFieldReferences(field, newName);
                field.Name = newName;
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

        var arrIdx = newName.IndexOf('[');
        if (arrIdx != -1)
        {
            newName = newName[..arrIdx];
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

    private static Utf8String? FieldNameToUpper(FieldDefinition field)
    {
        var fieldName = field.Name!.ToString();

        if (
            // Min length to rename
            fieldName.Length < 2
            || char.IsUpper(fieldName[0])
            // Don't bother with obfuscated names
            || fieldName.IsObfuscatedName()
            // No special names
            || fieldName.Contains('<')
            || fieldName.Contains('>')
            || field.IsPrivate
            || (field.DeclaringType?.IsGameObject() ?? false)
            || (field.Name!.StartsWith("_") && field.IsBackingField())
        )
        {
            return null;
        }

        if (fieldName[0] == '_')
        {
            fieldName = fieldName[1..];
        }

        return new Utf8String($"{char.ToUpper(fieldName[0])}{fieldName[1..]}");
    }

    private void UpdateFieldReferences(FieldDefinition field, Utf8String newName)
    {
        var references = memberReferenceCache.GetFieldReferences(field);

        foreach (var reference in references)
        {
            reference.Name = newName;
        }
    }
}
