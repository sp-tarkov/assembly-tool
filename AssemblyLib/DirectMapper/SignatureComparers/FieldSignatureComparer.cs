using AsmResolver.DotNet;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.SignatureComparers;

[Injectable]
public class FieldSigComparer
{
    public bool IsSame(FieldDefinition target, FieldDefinition dummy)
    {
        if (!CompareFieldContract(target, dummy))
        {
            return false;
        }

        var targetFieldType = target.Signature!.FieldType.Name;
        var dummyFieldType = dummy.Signature!.FieldType.Name;

        return targetFieldType == dummyFieldType;
    }

    private static bool CompareFieldContract(FieldDefinition target, FieldDefinition dummy)
    {
        return target.IsStatic == dummy.IsStatic
            && target.IsPublic == dummy.IsPublic
            && target.IsFamily == dummy.IsFamily
            && target.IsInitOnly == dummy.IsInitOnly;
    }
}
