using AsmResolver.DotNet;
using AssemblyLib.DirectMapper.NameFactory;
using AssemblyLib.DirectMapper.NameFactory.Renamers;
using AssemblyLib.Exceptions;
using AssemblyLib.Models;
using AssemblyLib.Shared;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable]
public class RenamerService(ILogger<RenamerService> logger, DataProvider dataProvider, IEnumerable<IRenamer> renamers)
{
    /// <summary>
    ///     Recursively rename the mapping file and all nested types
    /// </summary>
    /// <param name="targetFullName">Target GCType to rename</param>
    /// <param name="model">model</param>
    /// <param name="parent">parent used in recursive call</param>
    public Task RenameMappingRecursive(string targetFullName, DirectMapModel model, TypeDefinition? parent = null)
    {
        var toolData = model.ToolData;

        try
        {
            SetupToolData(targetFullName, model, parent);
        }
        catch (Exception ex)
        {
            logger.LogError("Error setting up tool data: {message}", ex.Message);
            return Task.CompletedTask;
        }

        if (toolData.Type is null)
        {
            logger.LogError("Failed to find type: {target}", targetFullName);
            return Task.CompletedTask;
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

                    logger.LogError(
                        "Failed to find nested type: {name} on parent {parent}",
                        name,
                        toolData.Type.FullName
                    );
                    logger.LogError("Available children for {parent}: {children}", toolData.Type.FullName, children);
                    continue;
                }

                RenameMappingRecursive(name, nestedModel, nestedType);
            }
        }

        RenameMapping(model);
        return Task.CompletedTask;
    }

    public void RenameCompilerGeneratedTypes()
    {
        if (renamers.FirstOrDefault(r => r is TypeRenamer) is not TypeRenamer classRenamer)
        {
            logger.LogError("Failed to find ClassRenamer type");
            return;
        }

        classRenamer.RenameCompilerGeneratedTypes();
    }

    public void PostDirectMapStage()
    {
        if (renamers.FirstOrDefault(r => r is FieldRenamer) is not FieldRenamer fieldRenamer)
        {
            logger.LogError("Failed to find FieldRenamer type");
            return;
        }

        fieldRenamer.RenameObfuscatedFields();
        fieldRenamer.FixCapitalizationOnPublicizedFields();
    }

    private void RenameMapping(DirectMapModel model)
    {
        foreach (var renamer in renamers.Where(r => r.Enabled).OrderByDescending(r => r.Priority))
        {
            renamer.Rename(model);
        }
    }

    private void SetupToolData(string targetFullName, DirectMapModel model, TypeDefinition? type = null)
    {
        var toolData = model.ToolData;

        toolData.Type =
            type ?? dataProvider.LoadedModule!.GetAllTypes().FirstOrDefault(t => t.FullName == targetFullName);

        if (toolData.Type is null)
        {
            throw new FailedToFindTypeException(
                $"Failed to find type: `{targetFullName}` in target assembly, names must be quantified by fullname including namespace or this is the wrong type."
            );
        }

        toolData.FullOldName = model.ToolData.Type?.FullName;
        toolData.ShortOldName = toolData.Type!.Name!.ToString();
    }
}
