using System.Linq;
using AsmResolver.DotNet;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Helpers;

[Injectable]
public sealed class DebuggableAttributeHelper(ILogger<DebuggableAttributeHelper> logger)
{
    private const string DebuggableAttributeFullName = "System.Diagnostics.DebuggableAttribute";

    public void RemoveDebuggableAttribute(ModuleDefinition module)
    {
        var assembly = module.Assembly;

        if (assembly is null)
        {
            return;
        }

        var attributes = assembly.CustomAttributes
            .Where(attribute => attribute.Constructor?.DeclaringType?.FullName == DebuggableAttributeFullName)
            .ToArray();

        foreach (var attribute in attributes)
        {
            assembly.CustomAttributes.Remove(attribute);
        }

        if (attributes.Length > 0)
        {
            logger.LogInformation(
                "Removed {Count} DebuggableAttribute assembly attribute(s).",
                attributes.Length
            );
        }
    }
}
