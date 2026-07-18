using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory.DirectMapRenamers;

[Injectable]
public class TypeDirectMapRenamer(DirectRenameCache directRenameCache) : IDirectMapRenamer
{
    public int Priority => -1;
    public bool Enabled => true;

    public ERenamerType Type => ERenamerType.Type;

    public void Rename(DirectMapModel model)
    {
        var toolData = model.ToolData;
        var wasRenamed = false;

        if (!string.IsNullOrEmpty(model.NewNamespace))
        {
            wasRenamed = toolData.Type?.Namespace?.ToString() != model.NewNamespace;
            toolData.Type?.Namespace = new Utf8String(model.NewNamespace);
        }

        // Not setting a new name
        if (model.NewName is null)
        {
            if (wasRenamed && toolData.Type is not null)
            {
                directRenameCache.Add(toolData.Type);
            }

            return;
        }

        var genericParametersCount = toolData.Type!.GenericParameters.Count;

        var utf8Name =
            genericParametersCount > 0
                ? new Utf8String($"{model.NewName!}`{genericParametersCount}")
                : new Utf8String(model.NewName!);

        wasRenamed |= toolData.Type?.Name != utf8Name;
        toolData.Type!.Name = utf8Name;

        if (wasRenamed)
        {
            directRenameCache.Add(toolData.Type);
        }
    }
}
