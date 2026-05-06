using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory.DirectMapRenamers;

[Injectable]
public class TypeDirectMapRenamer : IDirectMapRenamer
{
    public int Priority => -1;
    public bool Enabled => true;

    public ERenamerType Type => ERenamerType.Type;

    public void Rename(DirectMapModel model)
    {
        var toolData = model.ToolData;

        if (!string.IsNullOrEmpty(model.NewNamespace))
        {
            toolData.Type?.Namespace = new Utf8String(model.NewNamespace);
        }

        // Not setting a new name
        if (model.NewName is null)
        {
            return;
        }

        var genericParametersCount = toolData.Type!.GenericParameters.Count;

        var utf8Name =
            genericParametersCount > 0
                ? new Utf8String($"{model.NewName!}`{genericParametersCount}")
                : new Utf8String(model.NewName!);

        toolData.Type?.Name = utf8Name;
    }
}
