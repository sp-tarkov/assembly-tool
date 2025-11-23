using AsmResolver.DotNet;
using AssemblyLib.DirectMapper.Renamers;
using AssemblyLib.Models;
using AssemblyLib.Shared;
using Serilog;
using Serilog.Events;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable]
public class RenamerService(DataProvider dataProvider, IEnumerable<IRenamer> renamers)
{
    /// <summary>
    ///     Recursively rename the mapping file and all nested types
    /// </summary>
    /// <param name="targetFullName">Target GCType to rename</param>
    /// <param name="model">model</param>
    /// <param name="parent">parent used in recursive call, leave null</param>
    public void RenameMappingRecursive(string targetFullName, DirectMapModel model, TypeDefinition? parent = null)
    {
        var toolData = model.ToolData;

        toolData.Type =
            parent ?? dataProvider.LoadedModule!.GetAllTypes().FirstOrDefault(t => t.FullName == targetFullName);

        if (toolData.Type is null)
        {
            Log.Error("Failed to find type: {target}", targetFullName);
            return;
        }

        // Do children type's first so the parent can be used to find them
        if (model.NestedTypes is not null)
        {
            foreach (var (name, nestedModel) in model.NestedTypes)
            {
                var nestedType = toolData.Type.NestedTypes.FirstOrDefault(t => t.Name == name);
                if (nestedType is null)
                {
                    var children = string.Join(", ", nestedType?.NestedTypes.Select(t => t.Name?.ToString()) ?? []);

                    Log.Error("Failed to find nested type: {name} on parent {parent}", name, toolData.Type.FullName);
                    Log.Error("Available children for {parent}: {children}", toolData.Type.FullName, children);
                    continue;
                }

                RenameMappingRecursive(name, nestedModel, nestedType);
            }
        }

        // We're purely an entry for nested types. Do nothing else.
        if (model.NewName is null)
        {
            return;
        }

        RenameMapping(model);
    }

    public void RenameCompilerGeneratedTypes()
    {
        if (renamers.FirstOrDefault(r => r is ClassRenamer) is not ClassRenamer classRenamer)
        {
            Log.Error("Failed to find ClassRenamer type");
            return;
        }

        Log.Information("Renaming compiler generated types...");

        classRenamer.RenameCompilerGeneratedClasses();
    }

    private void RenameMapping(DirectMapModel model)
    {
        foreach (var renamer in renamers.OrderByDescending(r => r.Priority))
        {
            renamer.Rename(model);

            if (renamer.Type is not ERenamerType.Class)
            {
                continue;
            }

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                var toolData = model.ToolData;
                Log.Debug("Type: {old} -> {new}", toolData.FullOldName, toolData.Type?.FullName);
            }
        }
    }
}
