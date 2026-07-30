using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.Patching.MemberLookup;
using EFT.Console.Commands;
using EFT.HealthSystem;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching.ModulePatches.Fixes;

[Injectable]
public class ReflectionPatches(ILogger<ReflectionPatches> logger, ModuleMemberLookup lookup) : IModulePatch
{
    public bool Enabled => true;

    public void Patch()
    {
        PatchEffectActivator();
        PatchEffectTypeCode();
        PatchShared();
    }

    private void PatchEffectActivator()
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

        // 48 = Public | NonPublic
        ChangeBindingFlags(staticCtor, 32, 48);
    }

    private void PatchEffectTypeCode()
    {
        var type = lookup.Eft.Type<HealthHelper.EffectTypeCode>();
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

        // 48 = Public | NonPublic
        ChangeBindingFlags(staticCtor, 32, 48);
    }

    private void PatchShared()
    {
        var type = lookup.Eft.Type<Shared>();
        if (type is null)
        {
            throw new NullReferenceException("Could not find `Shared` when patching");
        }

        var staticCtor = type.GetStaticConstructor();
        if (staticCtor is null)
        {
            throw new NullReferenceException("Could not find static constructor for `Shared`");
        }

        // 52 = Public | NonPublic | Instance
        ChangeBindingFlags(staticCtor, 36, 52);
    }

    private void ChangeBindingFlags(MethodDefinition ctor, sbyte searchValue, sbyte newValue)
    {
        foreach (var instr in ctor.CilMethodBody!.Instructions)
        {
            // Look for ldc.i4 instruction that loads the BindingFlags value
            if (instr.OpCode != CilOpCodes.Ldc_I4_S || instr.Operand is not sbyte value || value != searchValue)
            {
                continue;
            }

            instr.Operand = newValue;
            return;
        }

        logger.LogError("Could not find BindingFlags to change");
    }
}
