using AssemblyLib.Patching;
using AssemblyLib.Patching.MemberLookup;
using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Fixes;

[Injectable]
public class DisableDevMaskCheckPatch(MemberLookup lookup, MethodBodyNuker methodBodyNuker) : IModulePatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var moveNextMethod = lookup.Eft.Method<LocalPlayer.CG_Struct0>("MoveNext");
        if (moveNextMethod is null)
        {
            throw new FailedToFindTypeException("Could not find `Eft.LocalPlayer.CG_Struct0.MoveNext()` when patching");
        }

        var body = moveNextMethod.CilMethodBody;
        var instructions = body!.Instructions;

        methodBodyNuker.NopRange(instructions, 365, 404);

        var handlerToRemove = body.ExceptionHandlers.FirstOrDefault(h =>
            h.TryStart?.Offset >= instructions[365].Offset && h.TryEnd?.Offset <= instructions[404].Offset + 1
        );

        if (handlerToRemove is not null)
        {
            body.ExceptionHandlers.Remove(handlerToRemove);
        }
    }
}
