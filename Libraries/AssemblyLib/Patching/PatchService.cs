using System.Reflection;
using AsmResolver.DotNet;
using AssemblyLib.Patching.Tool;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching;

[Injectable(InjectionType.Singleton)]
public class PatchService(
    ILogger<PatchService> logger,
    MemberLookup.ModuleMemberLookup lookup,
    MethodPatcher methodPatcher,
    IEnumerable<IModulePatch> modulePatches
)
{
    public void ApplyPatches()
    {
        ApplyModulePatches();
        ApplyMethodPatches();
    }

    private void ApplyModulePatches()
    {
        foreach (var patch in modulePatches.Where(p => p.Enabled))
        {
            patch.Patch();
            logger.LogInformation("Patch {patchName} applied.", patch.GetType().Name);
        }
    }

    private void ApplyMethodPatches()
    {
        var patchAssembly =
            AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "AssemblyLib.Patching")
            ?? throw new NullReferenceException("Could not find `AssemblyLib.Patching` assembly.");

        var patches = patchAssembly
            .GetTypes()
            .SelectMany(p => p.GetMethods().Where(m => m.GetCustomAttribute<PatchAttribute>() != null))
            .ToList();

        logger.LogInformation("Applying {count} method patches", patches.Count);

        foreach (var patch in patches)
        {
            var attr = patch.GetCustomAttribute<PatchAttribute>();
            if (attr is null)
            {
                throw new NullReferenceException("Could not find `MethodPatchAttribute`");
            }

            var targetMethod = ResolveTargetMethod(attr);
            var targetName = $"{attr.TargetType.Name}.{attr.MethodName}";
            if (targetMethod is null)
            {
                throw new NullReferenceException($"Could not find targetMethod: {targetName}()");
            }

            var sourceMethodDef = lookup.Stub.Method(patch.DeclaringType!, patch.Name);
            if (sourceMethodDef is null)
            {
                throw new NullReferenceException($"Could not find sourceMethodDef: {patch.Name}()");
            }

            methodPatcher.Patch(targetMethod, sourceMethodDef, attr.PatchType);
            logger.LogInformation("Applied PatchType: {patchType} to: {name}", attr.PatchType.ToString(), targetName);
        }
    }

    private MethodDefinition? ResolveTargetMethod(PatchAttribute attr) =>
        attr.TargetKind switch
        {
            PatchTargetKind.Method => lookup.Eft.Method(
                attr.TargetType,
                attr.MethodName,
                attr.TargetMethodParameterTypes
            ),
            PatchTargetKind.Constructor => lookup.Eft.Constructor(attr.TargetType, attr.TargetMethodParameterTypes),
            PatchTargetKind.StaticConstructor => lookup.Eft.StaticConstructor(attr.TargetType),
            _ => throw new NotImplementedException($"Method patch target kind '{attr.TargetKind}' is not implemented."),
        };
}
