using AsmResolver;
using AssemblyLib.Models;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Renamers;

[Injectable]
public class ClassRenamer : IRenamer
{
    public int Priority { get; } = 2;

    public ERenamerType Type
    {
        get { return ERenamerType.Class; }
    }

    public void Rename(DirectMapModel model)
    {
        var toolData = model.ToolData;

        toolData.FullOldName = model.ToolData.Type?.FullName;
        toolData.ShortOldName = toolData.Type!.Name!.ToString();

        toolData.Type?.Name = new Utf8String(model.NewName!);
    }
}
