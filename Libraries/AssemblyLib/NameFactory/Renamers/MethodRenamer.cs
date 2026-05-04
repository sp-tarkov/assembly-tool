using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Helpers;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory.Renamers;

[Injectable]
public class MethodRenamer(
    ILogger<MethodRenamer> logger,
    DataProvider dataProvider,
    MemberReferenceCache memberReferenceCache
) : IRenamer
{
    public int Priority => 0;
    public bool Enabled => true;

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

            var newName = new Utf8String($"{interfaceToRenameFor.Name}.{realMethodName}");
            method.Name = newName;
            UpdateMethodMemberReferences(method, newName);
        }
    }

    private void UpdateMethodMemberReferences(MethodDefinition target, Utf8String newName)
    {
        var cachedReferences = memberReferenceCache.GetMethodReferences(target);

        foreach (var reference in cachedReferences)
        {
            reference.Name = newName;
        }
    }
}
