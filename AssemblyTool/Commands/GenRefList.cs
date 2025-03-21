using AsmResolver.DotNet;
using AssemblyLib.Utils;
using AssemblyTool.Utils;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace AssemblyTool.Commands;

[Command("GenRefCountList", Description = "Generates a print out of the most used classes. Useful to prioritize remap targets")]
public class GenRefList : ICommand
{
    [CommandParameter(0, IsRequired = true, Description = "The absolute path to your de-obfuscated and remapped dll.")]
    public required string AssemblyPath { get; init; }

    private static readonly List<string> Match = new()
    {
        "Class",
        "GClass",
        "GControl",
        "GInterface",
        "Interface",
        "GStruct"
    };

    public ValueTask ExecuteAsync(IConsole console)
    {
        Debugger.TryWaitForDebuggerAttach();
        
        var references = CountTypeReferences(AssemblyPath);

        // Sort and display the top 10 most referenced types
        foreach (var pair in references.OrderByDescending(p => p.Value).Take(100))
        {
            console.Output.WriteLine($"{pair.Key}: {pair.Value}");
        }

        return default;
    }

    private static Dictionary<string, int> CountTypeReferences(string assemblyPath)
    {
        var typeReferenceCounts = new Dictionary<string, int>();

        var module = DataProvider.LoadModule(assemblyPath);
        
        foreach (var type in module.GetAllTypes())
        {
            CountReferencesInType(type, typeReferenceCounts);
        }

        return typeReferenceCounts;
    }

    private static void CountReferencesInType(TypeDefinition type, Dictionary<string, int> counts)
    {
        foreach (var method in type.Methods)
        {
            if (Match.Any(item => method.Signature.ReturnType.Name.StartsWith(item))) IncrementCount(method.Signature.ReturnType.Name, counts);

            CountReferencesInMethod(method, counts);
        }

        foreach (var field in type.Fields)
        {
            if (field.Signature.FieldType.IsValueType) continue;

            if (!Match.Any(item => field.Signature.FieldType.Name!.StartsWith(item))) continue;

            IncrementCount(field.Signature.FieldType.FullName, counts);
        }

        foreach (var property in type.Properties)
        {
            if (property.Signature.ReturnType.IsValueType) continue;

            if (!Match.Any(item => property.Signature.ReturnType.Name.StartsWith(item))) continue;

            IncrementCount(property.Signature.ReturnType.FullName, counts);
        }
    }

    private static void CountReferencesInMethod(MethodDefinition method, Dictionary<string, int> counts)
    {
        if (!method.HasMethodBody) return;

        foreach (var instr in method.CilMethodBody!.Instructions)
        {
            if (instr.Operand is FieldDefinition fieldDef && Match.Any(item => fieldDef.Signature!.FieldType.Name!.StartsWith(item)))
            {
                IncrementCount(fieldDef.Signature!.FieldType.FullName!, counts);
            }

            if (instr.Operand is PropertyDefinition propDef && Match.Any(item => propDef.Signature!.ReturnType!.FullName.StartsWith(item)))
            {
                IncrementCount(propDef.Signature!.ReturnType!.FullName, counts);
            }

            if (instr.Operand is MethodDefinition methodDef && Match.Any(item => methodDef.DeclaringType.FullName.StartsWith(item)))
            {
                if (methodDef.Signature!.ReturnType.IsValueType) continue;

                IncrementCount(methodDef.Signature.ReturnType.FullName, counts);
            }
        }
    }

    private static void IncrementCount(string typeName, Dictionary<string, int> counts)
    {
        counts[typeName] = counts.GetValueOrDefault(typeName, 0) + 1;
    }
}