using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.Patching.MemberLookup;
using JsonType;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Fixes;

/// <summary>
///     Change min required players for airdrops from 6 to 1
/// </summary>
[Injectable]
public class AllowAirdropsInPvePatch(MemberLookup lookup) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var type = lookup.Eft.Type<LocationSettings.Location.AirdropParameters>();
        if (type is null)
        {
            throw new NullReferenceException(
                "Could not find `LocationSettings.Location.AirdropParameters` when patching"
            );
        }

        var ctor = type.GetConstructor();
        if (ctor is null)
        {
            throw new NullReferenceException(
                "Could not find static constructor for `LocationSettings.Location.AirdropParameters`"
            );
        }

        foreach (var instr in ctor.CilMethodBody!.Instructions)
        {
            if (instr.OpCode != CilOpCodes.Ldc_I4_6)
            {
                continue;
            }

            instr.OpCode = CilOpCodes.Ldc_I4_1;
            break;
        }
    }
}
