using AsmResolver.DotNet;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.SignatureComparers;

[Injectable]
public class PropertySigComparer
{
    public bool IsSame(PropertyDefinition target, PropertyDefinition dummy)
    {
        return CompareGetMethods(target, dummy) && CompareSetMethods(target, dummy);
    }

    private static bool CompareGetMethods(PropertyDefinition target, PropertyDefinition dummy)
    {
        var targetGetter = target.GetMethod?.Signature;
        var dummyGetter = dummy.GetMethod?.Signature;

        return targetGetter?.ReturnsValue == dummyGetter?.ReturnsValue
            && targetGetter?.ReturnType.Name == dummyGetter?.ReturnType.Name;
    }

    private static bool CompareSetMethods(PropertyDefinition target, PropertyDefinition dummy)
    {
        var targetSetter = target.SetMethod;
        var dummySetter = dummy.SetMethod;

        return targetSetter?.IsPublic == dummySetter?.IsPublic
            && targetSetter?.IsAbstract == dummySetter?.IsAbstract
            && targetSetter?.IsStatic == dummySetter?.IsStatic
            && targetSetter?.IsVirtual == dummySetter?.IsVirtual
            && targetSetter?.IsSpecialName == dummySetter?.IsSpecialName
            && targetSetter?.GenericParameters.Count == dummySetter?.GenericParameters.Count;
    }
}
