using dnlib.DotNet;
using ReCodeItLib.Utils;

namespace ReCodeItLib.ReMapper;

internal class Publicizer
{
    public void PublicizeType(TypeDef type)
    {
        // if (type.CustomAttributes.Any(a => a.AttributeType.Name ==
        // nameof(CompilerGeneratedAttribute))) { return; }
        
        if (!type.IsNested && !type.IsPublic || type.IsNested && !type.IsNestedPublic)
        {
            if (!type.Interfaces.Any(i => i.Interface.Name == "IEffect"))
            {
                type.Attributes &= ~TypeAttributes.VisibilityMask; // Remove all visibility mask attributes
                type.Attributes |= type.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public; // Apply a public visibility attribute
            }
        }
        
        if (type.IsSealed)
        {
            type.Attributes &= ~TypeAttributes.Sealed; // Remove the Sealed attribute if it exists
        }
        
        foreach (var method in type.Methods)
        {
            PublicizeMethod(method);
        }
        
        foreach (var property in type.Properties)
        {
            if (property.GetMethod != null) PublicizeMethod(property.GetMethod);
            if (property.SetMethod != null) PublicizeMethod(property.SetMethod);
        }
        
        PublicizeFields(type);
        
        foreach (var nestedType in type.NestedTypes)
        {
            PublicizeType(nestedType);
        }
    }

    private static void PublicizeMethod(MethodDef method)
    {
        if (method.IsCompilerControlled /*|| method.CustomAttributes.Any(a => a.AttributeType.Name == nameof(CompilerGeneratedAttribute))*/)
        {
            return;
        }

        if (method.IsPublic) return;

        // if (!CanPublicizeMethod(method)) return;

        // Workaround to not publicize a specific method so the game doesn't crash
        if (method.Name == "TryGetScreen") return;

        method.Attributes &= ~MethodAttributes.MemberAccessMask;
        method.Attributes |= MethodAttributes.Public;
    }

    private static void PublicizeFields(TypeDef type)
    {
        ITypeDefOrRef declType = type.IsNested ? type : type.DeclaringType;
        while (declType is { FullName: 
                   not null 
                   and not "UnityEngine.Object" 
                   and not "Sirenix.OdinInspector.SerializedMonoBehaviour" }) 
        { declType = declType.GetBaseType(); }
        
        if (declType is not null)
        {
            Logger.LogSync($"Skipping {type.FullName} - object type {declType.FullName}");
            return;
        }
        
        foreach (var field in type.Fields)
        {
            if (field.IsPublic) continue;
            
            field.Attributes &= ~FieldAttributes.FieldAccessMask; // Remove all visibility mask attributes
            field.Attributes |= FieldAttributes.Public; // Apply a public visibility attribute
            
            // Ensure the field is NOT readonly
            field.Attributes &= ~FieldAttributes.InitOnly;

            if (field.CustomAttributes.Any(ca => ca.AttributeType.FullName 
                    is "UnityEngine.SerializeField" 
                    or "Newtonsoft.Json.JsonPropertyAttribute")) 
                continue;
            
            // Make sure we don't serialize this field.
            field.Attributes |= FieldAttributes.NotSerialized;
                
            Logger.LogSync($"Skipping {field.FullName} serialization");
        }
    }
}