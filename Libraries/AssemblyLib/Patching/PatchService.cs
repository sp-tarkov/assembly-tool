using System.Reflection;
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
        var methodPatches = AppDomain
            .CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "AssemblyLib.Patching")
            ?.GetTypes()
            .Where(t => t.GetMethods().Any(m => m.GetCustomAttribute<MethodPatchAttribute>() != null));

        var patches = methodPatches.SelectMany(p =>
            p.GetMethods().Where(m => m.GetCustomAttribute<MethodPatchAttribute>() != null)
        );

        logger.LogInformation("Applying {count} method patches", patches.Count());

        foreach (var patch in patches)
        {
            var attr = patch.GetCustomAttribute<MethodPatchAttribute>();
            if (attr is null)
            {
                throw new NullReferenceException("Could not find `MethodPatchAttribute`");
            }

            var targetMethod = lookup.Eft.Method(attr.TargetType, attr.MethodName, attr.TargetMethodParameterTypes);
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
}
