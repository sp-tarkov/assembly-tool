using AsmResolver.DotNet;
using AssemblyLib.Shared;

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
