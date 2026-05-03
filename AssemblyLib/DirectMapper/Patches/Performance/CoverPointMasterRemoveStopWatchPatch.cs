using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.Exceptions;
using AssemblyLib.Helpers;
using AssemblyLib.Patching.MemberLookup;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Performance;

/// <summary>
///     Get rid of stopwatches allocations
/// </summary>
/// <param name="lookup"></param>
[Injectable]
public class CoverPointMasterRemoveStopWatchPatch(MemberLookup lookup) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var body = lookup.Eft.Method<CoverPointMaster>("GetCoverPointMain2")?.CilMethodBody;
        if (body is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `Eft.CoverPointMaster.GetCoverPointMain2()` when patching"
            );
        }

        var instructions = body.Instructions;

        instructions[10].OpCode = CilOpCodes.Nop;
        instructions[11].OpCode = CilOpCodes.Nop;
        instructions[12].OpCode = CilOpCodes.Nop;
        instructions[69].OpCode = CilOpCodes.Nop;

        instructions.OptimizeMacros();
    }
}
