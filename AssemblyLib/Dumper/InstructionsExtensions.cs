/*

using dnlib.DotNet.Emit;

namespace AssemblyLib.Dumper;

public static class InstructionsExtensions
{
    public static void InsertBefore(this IList<Instruction> instructions, Instruction target, Instruction instruction)
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

    public static void InsertAfter(this IList<Instruction> instructions, Instruction target, Instruction instruction)
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

*/