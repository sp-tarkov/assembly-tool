using AsmResolver.PE.DotNet.Cil;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Reflection;

[Injectable]
public class EffectTypeCodeReflectionPatch(DataProvider dataProvider) : IPatch
{
    public bool Enabled => true;

    /// <summary>
    ///     Purpose of this patch is to set the BindingFlags to Public | NonPublic
    /// </summary>
    public void Patch()
    {
        var type = dataProvider
            .LoadedModule!.GetAllTypes()
            .FirstOrDefault(t => t.IsNested && t.Name == "EffectTypeCode");

        if (type is null)
        {
            throw new NullReferenceException("Could not find `EffectTypeCode` when patching");
        }

        var staticCtor = type.GetStaticConstructor();
        if (staticCtor is null)
        {
            throw new NullReferenceException("Could not find static constructor for `EffectTypeCode`");
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
            Log.Information("SharedReflectionPatch Successful");
            break;
        }
    }
}
