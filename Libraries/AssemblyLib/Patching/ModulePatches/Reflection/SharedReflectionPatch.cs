using AsmResolver.PE.DotNet.Cil;
using EFT.Console.Commands;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching.ModulePatches.Reflection;

[Injectable]
public class SharedReflectionPatch(MemberLookup.MemberLookup lookup) : IModulePatch
{
    public bool Enabled => true;

    public void Patch()
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

        foreach (var instr in staticCtor.CilMethodBody!.Instructions)
        {
            // Look for ldc.i4 instruction that loads the BindingFlags value

            if (instr.OpCode != CilOpCodes.Ldc_I4_S || instr.Operand is not sbyte value || value != 36)
            {
                continue;
            }

            // 52 = Public | NonPublic | Instance
            instr.Operand = (sbyte)52;
            break;
        }
    }
}
