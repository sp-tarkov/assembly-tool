using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Helpers;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory.DirectMapRenamers;

[Injectable]
public class FieldDirectMapRenamer(
    ILogger<FieldDirectMapRenamer> logger,
    DataProvider dataProvider,
    Statistics stats,
    MemberReferenceCache memberReferenceCache
) : IDirectMapRenamer
{
    public int Priority => 0;
    public bool Enabled => true;

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
