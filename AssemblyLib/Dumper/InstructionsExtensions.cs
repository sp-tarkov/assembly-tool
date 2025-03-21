using AsmResolver.PE.DotNet.Cil;

namespace AssemblyLib.Dumper;

public static class InstructionsExtensions
{
    public static void InsertBefore(this IList<CilInstruction> instructions, CilInstruction target, CilInstruction instruction)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof (target));
        }

        if (instruction == null)
        {
            throw new ArgumentNullException(nameof (instruction));
        }

        int index = instructions.IndexOf(target);
        if (index == -1)
        {
            throw new ArgumentOutOfRangeException(nameof (target));
        }

        instructions.Insert(index, instruction);
    }

    public static void InsertAfter(this IList<CilInstruction> instructions, CilInstruction target, CilInstruction instruction)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof (target));
        }

        if (instruction == null)
        {
            throw new ArgumentNullException(nameof (instruction));
        }

        int index = instructions.IndexOf(target);

        if (index == -1)
        {
            throw new ArgumentOutOfRangeException(nameof (target));
        }

        instructions.Insert(index + 1, instruction);
    }
}