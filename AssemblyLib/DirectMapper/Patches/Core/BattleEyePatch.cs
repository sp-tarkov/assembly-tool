using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.Patching;
using AssemblyLib.Patching.MemberLookup;
using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Core;

[Injectable]
public class BattleEyePatch(MemberLookup lookup, MethodBodyNuker methodBodyNuker) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var runValidationMethod = lookup.Eft.Method<AnticheatValidationOperation>("RunValidation");
        if (runValidationMethod?.CilMethodBody is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `Eft.AnticheatValidationOperation.RunValidation()` when patching"
            );
        }

        methodBodyNuker.NukeTaskBody(runValidationMethod.CilMethodBody);

        var bool0Field = lookup.Eft.Field<AnticheatValidationOperation>("bool_0");
        if (bool0Field is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `Eft.AnticheatValidationOperation.bool_0` when patching"
            );
        }

        var type = bool0Field.DeclaringType!;
        var ctors = type.Methods.Where(m => m.Name == ".ctor").ToList();

        foreach (var ctor in ctors)
        {
            var instructions = ctor.CilMethodBody!.Instructions;

            // Insert before the ret at the end
            var ret = instructions.Last(i => i.OpCode == CilOpCodes.Ret);
            var insertIndex = instructions.IndexOf(ret);

            instructions.Insert(insertIndex, new CilInstruction(CilOpCodes.Ldarg_0));
            instructions.Insert(insertIndex + 1, new CilInstruction(CilOpCodes.Ldc_I4_1));
            instructions.Insert(insertIndex + 2, new CilInstruction(CilOpCodes.Stfld, bool0Field));

            instructions.CalculateOffsets();
        }
    }
}
