using AsmResolver.PE.DotNet.Cil;
using EFT.HealthSystem;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching.Patches.Reflection;

[Injectable]
public class EffectActivatorReflectionPatch(MemberLookup.MemberLookup lookup) : IModulePatch
{
    public bool Enabled => true;

    /// <summary>
    ///     Purpose of this patch is to set the BindingFlags to Public | NonPublic
    /// </summary>
    public void Patch()
    {
        var type = lookup.Eft.Type<HealthHelper.EffectActivator<ActiveHealthController>>();
        if (type is null)
        {
            throw new NullReferenceException(
                "Could not find `HealthHelper.EffectActivator<ActiveHealthController>` when patching"
            );
        }

        var staticCtor = type.GetStaticConstructor();
        if (staticCtor is null)
        {
            throw new NullReferenceException(
                "Could not find static constructor for `HealthHelper.EffectActivator<ActiveHealthController>`"
            );
        }

        foreach (var instr in staticCtor.CilMethodBody!.Instructions)
        {
            // Look for ldc.i4 instruction that loads the BindingFlags value
            // BindingFlags.NonPublic = 32
            // BindingFlags.Public = 16

            if (instr.OpCode != CilOpCodes.Ldc_I4_S || instr.Operand is not sbyte value || value != 32)
            {
                continue;
            }

            // 48 = Public | NonPublic
            instr.Operand = (sbyte)48;
            break;
        }
    }
}
