using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Helpers;
using AssemblyLib.Models;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.NameFactory.Renamers;

[Injectable]
public class PropertyRenamer(ILogger<PropertyRenamer> logger, DataProvider dataProvider, Statistics stats) : IRenamer
{
    public int Priority => 0;
    public bool Enabled => false;

    public ERenamerType Type => ERenamerType.Properties;

    public void Rename(DirectMapModel model)
    {
        var toolData = model.ToolData;

        var propsToRename = model.PropertyRenames;
        if (propsToRename is null || propsToRename.Count == 0)
        {
            return;
        }

        foreach (var prop in toolData.Type!.Properties)
        {
            if (propsToRename.TryGetValue(prop.Name!.ToString(), out var newName))
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("\t\tProperty: {old} -> {new}", prop.Name.ToString(), newName);
                }

                prop.Name = new Utf8String(newName);
            }
        }
    }

    [Obsolete("Using BSG named properties now")]
    private void RenameObfuscatedProperties(ModuleDefinition module, Utf8String oldTypeName, Utf8String newTypeName)
    {
        foreach (var type in module.GetAllTypes())
        {
            var properties = type.Properties.Where(prop => prop.Name!.IsObfuscatedName());

            var propertyCount = 0;
            foreach (var property in properties)
            {
                if (property.Signature!.ReturnType.Name != newTypeName)
                {
                    continue;
                }

                var newPropertyName = GetNewPropertyName(newTypeName, propertyCount);

                // Dont need to do extra work
                if (property.Name == newPropertyName)
                {
                    continue;
                }

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
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

    private Utf8String GetNewPropertyName(string newName, int propertyCount = 0)
    {
        stats.PropertyRenamedCount++;

        return new Utf8String(propertyCount > 0 ? $"{newName}_{propertyCount}" : newName);
    }
}
