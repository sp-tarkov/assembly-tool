using AsmResolver.DotNet.Signatures;

namespace AssemblyLib.Extensions;

public static class TypeSignatureExtensions
{
    extension(TypeSignature signature)
    {
        public string GetFullTypeName()
        {
            switch (signature)
            {
                case GenericInstanceTypeSignature genericSig:
                {
                    var baseTypeName = genericSig.GenericType.FullName;
                    var genericArgs = string.Join(", ", genericSig.TypeArguments.Select(GetFullTypeName));
                    return $"{baseTypeName}<{genericArgs}>";
                }
                case TypeDefOrRefSignature typeDefOrRef:
                    return typeDefOrRef.FullName;
                default:
                    return signature.FullName;
            }
        }
    }
}
