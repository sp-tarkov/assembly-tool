using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching.ModulePatches.Core;


[Injectable]
public class BattleEyePatch(MemberLookup.ModuleMemberLookup lookup, MethodBodyNuker methodBodyNuker) : IModulePatch
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

        var succeedProperty = lookup.Eft.Property<AnticheatValidationOperation>("Succeed");
        if (succeedProperty?.GetMethod?.CilMethodBody is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `Eft.AnticheatValidationOperation.Succeed` when patching"
            );
        }

        var getterBody = succeedProperty
            .GetMethod.CilMethodBody.Instructions.Where(i => i.OpCode != CilOpCodes.Nop)
            .ToList();

        if (
            getterBody.Count != 3
            || getterBody[0].OpCode != CilOpCodes.Ldarg_0
            || getterBody[1].OpCode != CilOpCodes.Ldfld
            || getterBody[2].OpCode != CilOpCodes.Ret
            || getterBody[1].Operand is not FieldDefinition succeedField
            || succeedField.DeclaringType != succeedProperty.DeclaringType
        )
        {
            throw new FailedToFindTypeException(
                "Could not resolve the backing field of `Eft.AnticheatValidationOperation.Succeed` when patching"
            );
        }

        var type = succeedField.DeclaringType!;
        var ctors = type.Methods.Where(m => m.Name == ".ctor").ToList();

        foreach (var ctor in ctors)
        {
            var instructions = ctor.CilMethodBody!.Instructions;

            // Insert before the ret at the end
            var ret = instructions.Last(i => i.OpCode == CilOpCodes.Ret);
            var insertIndex = instructions.IndexOf(ret);

            instructions.Insert(insertIndex, new CilInstruction(CilOpCodes.Ldarg_0));
            instructions.Insert(insertIndex + 1, new CilInstruction(CilOpCodes.Ldc_I4_1));
            instructions.Insert(insertIndex + 2, new CilInstruction(CilOpCodes.Stfld, succeedField));

            instructions.CalculateOffsets();
        }
    }
}
