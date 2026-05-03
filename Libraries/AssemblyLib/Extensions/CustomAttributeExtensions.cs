using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace AssemblyLib.Extensions;

public static class CustomAttributeExtensions
{
    extension(CustomAttribute attr)
    {
        public bool IsAsyncStateMachineAttribute()
        {
            return attr.Constructor?.DeclaringType?.FullName
                   == "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
        }

        public bool IsJsonConverterAttribute()
        {
            var fullName = attr.Constructor?.DeclaringType?.FullName;
            return fullName == "Newtonsoft.Json.JsonConverterAttribute";
        }

        public bool IsTypeConverterAttribute()
        {
            var fullName = attr.Constructor?.DeclaringType?.FullName;
            return fullName == "System.ComponentModel.TypeConverterAttribute";
        }
        
        public string? ExtractTypeNameFromAttribute()
        {
            if (attr.Signature?.FixedArguments.Count == 0)
            {
                return null;
            }

            var argument = attr.Signature?.FixedArguments[0];

            return argument?.Element switch
            {
                TypeDefOrRefSignature typeSig => typeSig.GetFullTypeName(),
                ITypeDescriptor typeDesc => typeDesc.FullName,
                _ => null,
            };
        }
    }
}
