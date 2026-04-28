using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.Exceptions;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Fixes;

[Injectable]
public class DisableDevMaskCheckPatch(DataProvider dataProvider) : IPatch
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
            throw new FailedToFindTypeException(
                "Could not find `Eft.CreateProfileOperation.CG_Struct0.MoveNext()` when patching"
            );
        }

        var instructions = body.Instructions;

        for (var i = 365; i <= 404; i++)
        {
            instructions[i].OpCode = CilOpCodes.Nop;
            instructions[i].Operand = null;
        }

        var handlerToRemove = body.ExceptionHandlers.FirstOrDefault(h =>
            h.TryStart?.Offset >= instructions[365].Offset && h.TryEnd?.Offset <= instructions[404].Offset + 1
        );

        if (handlerToRemove is not null)
            body.ExceptionHandlers.Remove(handlerToRemove);

        Log.Information("DeveloperCheckPatch Successful");
    }
}
