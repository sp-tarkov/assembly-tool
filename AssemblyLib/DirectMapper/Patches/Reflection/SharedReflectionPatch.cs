using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Reflection;

[Injectable]
public class SharedReflectionPatch(DataProvider dataProvider) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var type = dataProvider.LoadedModule!.GetAllTypes().FirstOrDefault(t => t.Name == "Shared");

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
            Log.Information("SharedReflectionPatch Successful");
            break;
        }
    }
}
