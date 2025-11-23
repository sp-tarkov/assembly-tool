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
public class PropertyRenamer(DataProvider dataProvider, Statistics stats) : IRenamer
{
    public int Priority { get; } = int.MinValue;

    public ERenamerType Type
    {
        get { return ERenamerType.Properties; }
    }

    public void Rename(DirectMapModel model)
    {
        RenameObfuscatedProperties(dataProvider.LoadedModule!, model.ToolData.ShortOldName!, model.NewName!);
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

    private Utf8String GetNewPropertyName(string newName, int propertyCount = 0)
    {
        stats.PropertyRenamedCount++;

        return new Utf8String(propertyCount > 0 ? $"{newName}_{propertyCount}" : newName);
    }
}
