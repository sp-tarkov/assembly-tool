using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.Exceptions;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Performance;

[Injectable]
public class CoverPointMasterRemoveStopWatchPatch(DataProvider dataProvider) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var body = dataProvider
            .LoadedModule!.GetAllTypes()
            .FirstOrDefault(t => t.Name == "CoverPointMaster")
            ?.Methods.FirstOrDefault(m => m.Name == "GetCoverPointMain2")
            ?.CilMethodBody;

        if (body is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `Eft.CoverPointMaster.GetCoverPointMain2()` when patching"
            );
        }

        var instructions = body.Instructions;

        instructions[69].OpCode = CilOpCodes.Nop;

        instructions[12].OpCode = CilOpCodes.Nop;
        instructions[11].OpCode = CilOpCodes.Nop;
        instructions[10].OpCode = CilOpCodes.Nop;

        instructions.OptimizeMacros();

        Log.Information("CoverPointMasterRemoveStopWatchPatch Successful");
    }
}
