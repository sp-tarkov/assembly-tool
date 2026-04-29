using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Extensions;
using AssemblyLib.Models;
using AssemblyLib.Shared;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Renamers;

[Injectable]
public class MethodRenamer(ILogger<MethodRenamer> logger, DataProvider dataProvider) : IRenamer
{
    public int Priority => 0;
    public bool Enabled => false;

    public ERenamerType Type => ERenamerType.Methods;

    public void Rename(DirectMapModel model)
    {
        var toolData = model.ToolData;

        if (toolData.Type?.IsInterface ?? false)
        {
            RenameInterfacePrefacedMethods(toolData.Type);
        }

        var methodsToRename = model.MethodRenames;
        if (methodsToRename is null || methodsToRename.Count == 0)
        {
            return;
        }

        foreach (var method in toolData.Type!.Methods)
        {
            if (method.IsCompilerGenerated() || method.IsGetMethod || method.IsSetMethod)
            {
                continue;
            }

            if (methodsToRename.TryGetValue(method.Name!.ToString(), out var newName))
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("\t\tMethod: {old} -> {new}", method.Name.ToString(), newName);
                }

                method.Name = new Utf8String(newName);
            }
        }
    }

    private void RenameInterfacePrefacedMethods(TypeDefinition interfaceToRenameFor)
    {
        var implementations = dataProvider
            .LoadedModule!.GetAllTypes()
            .Where(t => t.Implements(interfaceToRenameFor.FullName));

        if (!implementations.Any())
        {
            return;
        }

        var interfaceMethodNames = interfaceToRenameFor.Methods.Select(t => t.Name!.ToString()).ToArray();

        foreach (var method in implementations.SelectMany(t => t.Methods))
        {
            var methodSplitName = method.Name!.Split('.');
            if (methodSplitName.Length <= 1)
            {
                continue;
            }

            var realMethodName = methodSplitName.Last();

            // Not a method impl from this interface
            if (!interfaceMethodNames.Contains(realMethodName))
            {
                continue;
            }

            method.Name = new Utf8String($"{interfaceToRenameFor.Name}.{realMethodName}");
        }
    }
}
