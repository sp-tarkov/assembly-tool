using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Serilog;

namespace AssemblyLib.Extensions;

internal static class MethodDefExtensions
{
    extension(MethodDefinition methodDef)
    {
        public bool IsExplicitInterfaceImplementation()
        {
            return methodDef.DeclaringType?.MethodImplementations
                .Any(impl => impl.Body?.Resolve(DataProvider.Instance.Context) == methodDef) ?? false;
        }
        
        public IMethodDefOrRef? GetExplicitInterfaceTarget()
        {
            return methodDef.DeclaringType?.MethodImplementations
                .FirstOrDefault(impl => impl.Body?.Resolve(DataProvider.Instance.Context) == methodDef)
                .Declaration;
        }
        
        public bool IsImplicitInterfaceImplementation()
        {
            // Only public, non-static methods can implicitly implement interfaces
            if (!methodDef.IsPublic || methodDef.IsStatic)
            {
                return false;
            }

            foreach (var ifaceImpl in methodDef.DeclaringType?.Interfaces ?? [])
            {
                var ifaceType = ifaceImpl.Interface?.Resolve(DataProvider.Instance.Context);
                if (ifaceType is null)
                {
                    continue;
                }

                foreach (var ifaceMethod in ifaceType.Methods)
                {
                    if (ifaceMethod.Name == methodDef.Name && SignaturesMatch(ifaceMethod, methodDef))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        
        public void DumpMethodInstructions()
        {
            var instructions = methodDef.CilMethodBody!.Instructions;

            Log.Information("=== {method} IL Dump ===", methodDef.Name!.ToString());
            for (var i = 0; i < instructions.Count; i++)
            {
                Log.Information(
                    "[{I}] {Offset:X4}: {CilOpCode} {Operand} (type: {Name})",
                    i,
                    instructions[i].Offset,
                    instructions[i].OpCode,
                    instructions[i].Operand,
                    instructions[i].Operand?.GetType().Name
                );
            }
        }
        
        public bool IsAsyncMethod()
        {
            return methodDef
                .CustomAttributes.Select(s => s.Constructor?.DeclaringType?.FullName)
                .Contains("System.Runtime.CompilerServices.AsyncStateMachineAttribute");
        }
        
        public bool IsVoidWithNoParameters()
        {
            return !methodDef.Signature!.ReturnsValue && methodDef.Parameters.Count == 0;
        }

        public bool HasGenericParameters()
        {
            return methodDef.Parameters
                .Any(p => p.ParameterType is GenericParameterSignature);
        }

        public bool IsVoidWithOnlyGenericParameters()
        {
            return !methodDef.Signature!.ReturnsValue && methodDef.Parameters.All(p => p.ParameterType is GenericParameterSignature);
        }
    }
    
    private static bool SignaturesMatch(MethodDefinition a, MethodDefinition b)
    {
        if (a.Parameters.Count != b.Parameters.Count)
        {
            return false;
        }

        if (a.Signature?.ReturnType.FullName != b.Signature?.ReturnType.FullName)
        {
            return false;
        }

        for (var i = 0; i < a.Parameters.Count; i++)
        {
            if (a.Parameters[i].ParameterType.FullName != b.Parameters[i].ParameterType.FullName)
            {
                return false;
            }
        }

        return true;
    }
}
