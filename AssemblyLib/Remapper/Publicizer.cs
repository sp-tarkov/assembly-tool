using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AssemblyLib.Utils;
using Serilog;
using Serilog.Events;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper;

[Injectable]
public sealed class Publicizer(Statistics stats) 
{
    /// <summary>
    /// Publicize the provided type
    /// </summary>
    /// <param name="type">Type to publicize</param>
    /// <returns>Dictionary of publicized fields Key: Field Val: IsProtected</returns>
    public List<FieldDefinition> PublicizeType(TypeDefinition type)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Publicizing Type [{Utf8String}]", type.Name?.ToString());
        }
        
        if (type is { IsNested: false, IsPublic: false } or { IsNested: true, IsNestedPublic: false }
            && type.Interfaces.All(i => i.Interface?.Name != "IEffect"))
        {
            type.Attributes &= ~TypeAttributes.VisibilityMask; // Remove all visibility mask attributes
            type.Attributes |= type.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public; // Apply a public visibility attribute
            stats.TypePublicizedCount++;
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
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Publicizing Property [{PropertyDeclaringType}::{PropertyName}]", 
                    property.DeclaringType,
                    property.Name?.ToString()
                );
            }
            
            // TODO: This is hacky but works for now, find a better solution. Need to check MD tokens to build associations,
            // this is a problem for later me.
            
            // NOTE: Ignore properties that are interface impls that are private.
            // This causes issues with json deserialization in the server.
            if (property.Name?.Contains(".") ?? false) continue;
            
            if (property.GetMethod != null) PublicizeMethod(property.GetMethod);
            if (property.SetMethod != null) PublicizeMethod(property.SetMethod);

            stats.PropertyPublicizedCount++;
        }
        
        return PublicizeFields(type);
    }

    private void PublicizeMethod(MethodDefinition method)
    {
        if (method.IsCompilerControlled || method.IsPublic) return;
        
        // Workaround to not publicize a specific method so the game doesn't crash
        if (method.Name == "TryGetScreen") return;
        
        method.Attributes &= ~MethodAttributes.MemberAccessMask;
        method.Attributes |= MethodAttributes.Public;

        if (method.IsGetMethod || method.IsSetMethod) return;

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Publicizing Method [{MethodDeclaringType}::{MethodName}]", 
                method.DeclaringType,
                method.Name?.ToString()
            );
        }
        
        stats.MethodPublicizedCount++;
    }

    private List<FieldDefinition> PublicizeFields(TypeDefinition type)
    {
        if (!ShouldPublicizeFields(type))
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Skipping field publication on [{Utf8String}]", type.Name?.ToString());
            }
            
            return [];
        }
        
        var result = new List<FieldDefinition>();
        foreach (var field in type.Fields)
        {
            if (field.IsPublic || IsEventField(type, field)) continue;

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Publicizing Field [{FieldDeclaringType}::{Utf8String}]", 
                    field.DeclaringType, 
                    field.Name?.ToString()
                );
            }
            
            stats.FieldPublicizedCount++;
            field.Attributes &= ~FieldAttributes.FieldAccessMask; // Remove all visibility mask attributes
            field.Attributes |= FieldAttributes.Public; // Apply a public visibility attribute
            
            // Ensure the field is NOT readonly
            field.Attributes &= ~FieldAttributes.InitOnly;
            
            result.Add(field);
            
            if (field.HasCustomAttribute("UnityEngine", "SerializeField") ||
                field.HasCustomAttribute("Newtonsoft.Json", "JsonPropertyAttribute"))
                continue;
                
            // Make sure we don't serialize this field.
            // TODO: Do we need this?
            field.Attributes |= FieldAttributes.NotSerialized;
        }

        return result;
    }

    private static bool ShouldPublicizeFields(TypeDefinition type)
    {
        return !type.InheritsFrom("UnityEngine", "Object") && 
               !type.InheritsFrom("Sirenix.OdinInspector", "SerializedMonoBehaviour");
    }
    
    private static bool IsEventField(TypeDefinition type, FieldDefinition field)
    {
        // TODO: This can be cleaned up, redundant code.
        foreach (var evt in type.Events)
        {
            if (evt.AddMethod is { CilMethodBody: not null })
            {
                if (IsMemberReferenceNameMatch(evt.AddMethod.CilMethodBody.Instructions, field.Name))
                {
                    return true;
                }
            }
                
            if (evt.RemoveMethod is { CilMethodBody: not null })
            {
                if (IsMemberReferenceNameMatch(evt.RemoveMethod.CilMethodBody.Instructions, field.Name))
                {
                    return true;
                }
            }
                
            if (evt.FireMethod is { CilMethodBody: not null })
            {
                if (IsMemberReferenceNameMatch(evt.FireMethod.CilMethodBody.Instructions, field.Name))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    private static bool IsMemberReferenceNameMatch(CilInstructionCollection instructions, Utf8String? memberName)
    {
        foreach (var instr in instructions)
        {
            if (instr.Operand is FieldDefinition fieldDefinition && fieldDefinition.Name == memberName)
            {
                return true;
            }
        }
        
        return false;
    }
}