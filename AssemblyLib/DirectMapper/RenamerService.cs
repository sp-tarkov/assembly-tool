using AssemblyLib.DirectMapper.Renamers;
using AssemblyLib.Models;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable]
public class RenamerService(IEnumerable<IRenamer> renamers)
{
    public void RenameMapping(DirectMapModel model)
    {
        foreach (var renamer in renamers.OrderByDescending(r => r.Priority))
        {
            renamer.Rename(model);

            if (renamer.Type is not ERenamerType.Class)
            {
                continue;
            }

            var toolData = model.ToolData;
            Log.Information("Type: {old} -> {new}", toolData.FullOldName, toolData.Type?.FullName);
        }
    }
}
