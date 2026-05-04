using AsmResolver.DotNet;
using AssemblyLib.SignatureComparers;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory.SigRenamers;

[Injectable]
public class PropertySigRenamer(
    ILogger<PropertySigRenamer> logger,
    DataProvider dataProvider,
    PropertySigComparer propertySigComparer
) : ISigRenamer
{
    public int Priority => 0;
    public bool Enabled => true;
    public ERenamerType Type => ERenamerType.Properties;

    public void Rename(TypeDefinition targetType, TypeDefinition dummyType)
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

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "Renaming property: {old} -> {new}",
                        targetProperty.FullName,
                        dummyProperty.FullName
                    );
                }

                targetProperty.Name = dummyProperty.Name;
                dummyProperties.Remove(dummyProperty);
                break;
            }
        }
    }
}
