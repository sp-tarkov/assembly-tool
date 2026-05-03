using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.SignatureComparers;

[Injectable]
public class MethodSigComparer
{
    public bool IsSame(MethodDefinition target, MethodDefinition dummy)
    {
        if (!CompareMethodContract(target, dummy))
        {
            return false;
        }

        if (!CompareMethodSignatures(target.Signature!, dummy.Signature!))
        {
            return false;
        }

        if (!CompareMethodParameters(target, dummy))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Checks if the method is a void return type method with no parameters
    /// </summary>
    /// <param name="target">method to check</param>
    /// <returns>True if method returns no value and has no parameters</returns>
    public bool IsVoidMethodWithNoParameters(MethodDefinition target)
    {
        return !target.Signature!.ReturnsValue && target.Parameters.Count == 0;
    }

    private static bool CompareMethodContract(MethodDefinition target, MethodDefinition dummy)
    {
        return target.IsAbstract == dummy.IsAbstract
            && target.IsVirtual == dummy.IsVirtual
            && target.IsPublic == dummy.IsPublic;
    }

    private static bool CompareMethodSignatures(MethodSignature target, MethodSignature dummy)
    {
        return target.GenericParameterCount == dummy.GenericParameterCount
            && target.HasThis == dummy.HasThis
            && target.ReturnsValue == dummy.ReturnsValue
            && target.ReturnType.FullName == dummy.ReturnType.FullName
            && target.GetTotalParameterCount() == dummy.GetTotalParameterCount()
            && target.SentinelParameterTypes.Count == dummy.SentinelParameterTypes.Count;
    }

    private static bool CompareMethodParameters(MethodDefinition target, MethodDefinition dummy)
    {
        var targetParms = target.Parameters;
        var dummyParms = dummy.Parameters;

        if (targetParms.Count != dummyParms.Count)
        {
            return false;
        }

        for (var i = 0; i < targetParms.Count; i++)
        {
            var parm1 = targetParms[i];
            var parm2 = dummyParms[i];

            if (parm1.Name != parm2.Name)
            {
                return false;
            }

            if (parm1.ParameterType.FullName != parm2.ParameterType.FullName)
            {
                return false;
            }
        }

        return true;
    }
}
