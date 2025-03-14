using dnlib.DotNet;
using ReCodeItLib.Utils;

namespace ReCodeItLib.ReMapper;

internal class Publicizer(Statistics stats)
{
    public void PublicizeType(TypeDef type)
    {
        // if (type.CustomAttributes.Any(a => a.AttributeType.Name ==
        // nameof(CompilerGeneratedAttribute))) { return; }
        
        if (type is { IsNested: false, IsPublic: false } or { IsNested: true, IsNestedPublic: false })
        {
            if (type.Interfaces.All(i => i.Interface.Name != "IEffect"))
            {
                type.Attributes &= ~TypeAttributes.VisibilityMask; // Remove all visibility mask attributes
                type.Attributes |= type.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public; // Apply a public visibility attribute
                stats.TypePublicizedCount++;
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
            if (property.GetMethod != null) PublicizeMethod(property.GetMethod, true);
            if (property.SetMethod != null) PublicizeMethod(property.SetMethod, true);

            stats.PropertyPublicizedCount++;
        }
        
        PublicizeFields(type);
    }

    private void PublicizeMethod(MethodDef method, bool isProperty = false)
    {
        if (method.IsCompilerControlled)
        {
            return;
        }

        if (method.IsPublic) return;
        
        // Workaround to not publicize a specific method so the game doesn't crash
        if (method.Name == "TryGetScreen") return;

        method.Attributes &= ~MethodAttributes.MemberAccessMask;
        method.Attributes |= MethodAttributes.Public;

        if (isProperty) return;

        stats.MethodPublicizedCount++;
    }

    private void PublicizeFields(TypeDef type)
    {
        ITypeDefOrRef declType = type.IsNested ? type.DeclaringType : type;
        while (declType is { FullName: 
                   not null 
                   and not "UnityEngine.Object" 
                   and not "Sirenix.OdinInspector.SerializedMonoBehaviour" }) 
        { declType = declType.GetBaseType(); }
        
        if (declType is not null)
        {
            //Logger.LogSync($"Skipping {type.FullName} - object type {declType.FullName}");
            return;
        }
        
        foreach (var field in type.Fields)
        {
            if (field.IsPublic || IsEventField(type, field)) continue;
            
            stats.FieldPublicizedCount++;
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
                
            //Logger.LogSync($"Skipping {field.FullName} serialization");
        }
    }

    private static bool IsEventField(TypeDef type, FieldDef field)
    {
        foreach (var evt in type.Events)
        {
            if (evt.AddMethod != null && evt.AddMethod.Body != null)
            {
                foreach (var instr in evt.AddMethod.Body.Instructions)
                {
                    if (instr.Operand is FieldDef fieldDef && fieldDef == field)
                    {
                        return true;
                    }
                }
            }
                
            if (evt.RemoveMethod != null && evt.RemoveMethod.Body != null)
            {
                foreach (var instr in evt.RemoveMethod.Body.Instructions)
                {
                    if (instr.Operand is FieldDef fieldDef && fieldDef == field)
                    {
                        return true;
                    }
                }
            }
                
            if (evt.InvokeMethod != null && evt.InvokeMethod.Body != null)
            {
                foreach (var instr in evt.InvokeMethod.Body.Instructions)
                {
                    if (instr.Operand is FieldDef fieldDef && fieldDef == field)
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
}