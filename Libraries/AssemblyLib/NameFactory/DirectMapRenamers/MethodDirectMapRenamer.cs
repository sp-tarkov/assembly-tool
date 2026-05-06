using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Helpers;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory.DirectMapRenamers;

[Injectable]
public class MethodDirectMapRenamer(
    ILogger<MethodDirectMapRenamer> logger,
    DataProvider dataProvider,
    MemberReferenceCache memberReferenceCache
) : IDirectMapRenamer
{
    public int Priority => 0;
    public bool Enabled => true;

    public ERenamerType Type => ERenamerType.Methods;

    public void Rename(DirectMapModel model)
    {
        var toolData = model.ToolData;

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

                var utf8NewName = new Utf8String(newName);
                method.Name = utf8NewName;
                UpdateMethodMemberReferences(method, utf8NewName);
            }
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
