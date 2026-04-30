using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.DirectMapper.Helpers;
using AssemblyLib.Exceptions;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Fixes;

[Injectable]
public class DisableDevMaskCheckPatch(PatchHelper patchHelper, DataProvider dataProvider) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var body = dataProvider
            .LoadedModule!.GetAllTypes()
            .FirstOrDefault(t => t.Namespace == "EFT" && t.Name == "LocalPlayer")
            ?.NestedTypes.FirstOrDefault(t => t.IsValueType)
            ?.Methods.FirstOrDefault(m => m.Name == "MoveNext")
            ?.CilMethodBody;

        if (body is null)
        {
            throw new FailedToFindTypeException("Could not find `Eft.LocalPlayer.CG_Struct0.MoveNext()` when patching");
        }

        var instructions = body.Instructions;

        patchHelper.NopRange(instructions, 365, 404);

        var handlerToRemove = body.ExceptionHandlers.FirstOrDefault(h =>
            h.TryStart?.Offset >= instructions[365].Offset && h.TryEnd?.Offset <= instructions[404].Offset + 1
        );

        if (handlerToRemove is not null)
        {
            body.ExceptionHandlers.Remove(handlerToRemove);
        }

        Log.Information("DisableDevMaskCheckPatch Successful");
    }
}
