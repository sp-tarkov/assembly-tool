using System.Reflection;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.Application;
using AssemblyLib.Models;
using AssemblyLib.Utils;
using FieldDefinition = AsmResolver.DotNet.FieldDefinition;
using MemberReference = AsmResolver.DotNet.MemberReference;
using MethodDefinition = AsmResolver.DotNet.MethodDefinition;
using TypeDefinition = AsmResolver.DotNet.TypeDefinition;

namespace AssemblyLib.ReMapper;

internal sealed class Renamer(ModuleDefinition module, List<TypeDefinition> types, Statistics stats) 
    : IComponent
{
    public void RenamePublicizedFieldAndUpdateMemberRefs(FieldDefinition fieldDef)
    {
        var origName = fieldDef.Name?.ToString();
        
        var newName = string.Empty;
        
        if (origName is null || origName.Length < 3 || IsSerializedField(fieldDef)) return;

        // Handle underscores
        if (origName[0] == '_')
        {
            newName = char.ToUpper(origName[1]) + origName[2..];
        }

        if (char.IsLower(origName[0]))
        {
            newName = char.ToUpper(origName[0]) + origName[1..];
        }

        if (newName == string.Empty) return;
        
        var fields = fieldDef.DeclaringType?.Fields;
        var props = fieldDef.DeclaringType?.Properties;
        
        if ((fields is not null && fields.Any(f => f.Name == newName)) || 
            (props is not null && props.Any(p => p.Name == newName)))
        {
            newName += "_1";
        }
        
        Logger.Log($"Renaming Publicized field [{fieldDef.DeclaringType}::{origName}] to [{fieldDef.DeclaringType}::{newName}]", 
            ConsoleColor.Green,
            true);

        var newUtf8Name = new Utf8String(newName);
        
        UpdateMemberReferences(fieldDef, newUtf8Name);
        fieldDef.Name = newUtf8Name;
    }
    
    public void RenameRemap(RemapModel remap)
    {
        // Rename all fields and properties first
        
        // TODO: Passing strings as Utf8String, fix the model
        RenameObfuscatedFields(
            remap.TypePrimeCandidate!.Name!,
            remap.NewTypeName);
        
        RenameObfuscatedProperties(
            remap.TypePrimeCandidate!.Name!,
            remap.NewTypeName);
        
        remap.TypePrimeCandidate.Name = new Utf8String(remap.NewTypeName);
    }

    public void FixInterfaceMangledMethodNames()
    {
        // We're only looking for implementations
        foreach (var type in types.Where(t => !t.IsInterface))
        {
            var renamedMethodNames = new List<Utf8String>();
            foreach (var method in type.Methods)
            {
                if (method.IsConstructor || method.IsSetMethod || method.IsGetMethod) continue;
                
                var newMethodName = FixInterfaceMangledMethod(method, renamedMethodNames);
                
                if (newMethodName == Utf8String.Empty) continue;
                
                renamedMethodNames.Add(newMethodName);
            }
        }
    }
    
    private static Utf8String FixInterfaceMangledMethod(MethodDefinition method, List<Utf8String> renamedMethodsOnType)
    {
        var splitName = method.Name?.Split('.');
        
        if (splitName is null || splitName.Length < 2) return Utf8String.Empty;

        var realMethodNameString = splitName.Last();
        var newName = new Utf8String(realMethodNameString);
        var sameNameCount = renamedMethodsOnType.Count(c => c == newName);
        
        if (sameNameCount > 0)
        {
            newName = new Utf8String($"{realMethodNameString}_{sameNameCount}");
        }

        if (method.DeclaringType!.Methods.Any(m => m.Name == realMethodNameString))
        {
            newName = new Utf8String($"{realMethodNameString}_1");
        }
        
        Logger.Log($"Renaming method [{method.DeclaringType}::{method.Name}] to [{method.DeclaringType}::{newName}]");
        
        method.Name = newName;
        
        return newName;
    }
    
    private void RenameObfuscatedFields(Utf8String oldTypeName, Utf8String newTypeName)
    {
        foreach (var type in types)
        {
            var fields = type.Fields
                .Where(field => field.Name!.IsObfuscatedName());
            
            var fieldCount = 0;
            foreach (var field in fields)
            {
                if (field.Signature?.FieldType.Name != oldTypeName) continue;
                
                var newFieldName = GetNewFieldName(field, newTypeName, fieldCount);

                // Dont need to do extra work
                if (field.Name == newFieldName) { continue; }
                
                var oldName = field.Name;

                Logger.Log($"Renaming field [{field.DeclaringType}::{oldName}] to [{field.DeclaringType}::{newFieldName}]", 
                    diskOnly: true);
                
                fieldCount++;
                
                UpdateMemberReferences(field, newFieldName);
                field.Name = newFieldName;
            }
        }
    }
    
    private void RenameObfuscatedProperties(Utf8String oldTypeName, Utf8String newTypeName)
    {
        foreach (var type in types)
        {
            var properties = type.Properties
                .Where(prop => prop.Name!.IsObfuscatedName());
            
            var propertyCount = 0;
            foreach (var property in properties)
            {
                if (property.Signature!.ReturnType.Name != oldTypeName) continue;
                
                var newPropertyName = GetNewPropertyName(newTypeName, propertyCount);

                // Dont need to do extra work
                if (property.Name == newPropertyName) continue; 
                    
                Logger.Log($"Renaming property [{property.DeclaringType}::{property.Name}] to [{property.DeclaringType}::{newPropertyName}]", 
                    diskOnly: true);
                
                property.Name = newPropertyName;

                propertyCount++;
            }
        }
    }

    private Utf8String GetNewFieldName(FieldDefinition field, string newName, int fieldCount = 0)
    {
        var newFieldCount = fieldCount > 0 ? $"_{fieldCount}" : string.Empty;

        var firstChar = field.IsPublic
            ? char.ToUpper(newName[0])
            : char.ToLower(newName[0]);
        
        stats.FieldRenamedCount++;
        return new Utf8String($"{firstChar}{newName[1..]}{newFieldCount}");
    }

    private Utf8String GetNewPropertyName(string newName, int propertyCount = 0)
    {
        stats.PropertyRenamedCount++;
        
        return new Utf8String(propertyCount > 0 ? $"{newName}_{propertyCount}" : newName);
    }

    private void UpdateMemberReferences(
        FieldDefinition target,
        Utf8String newName)
    {
        foreach (var reference in module.GetImportedMemberReferences())
        {
            if (reference.Resolve() == target)
            {
                Logger.Log($"Updating Field Reference to [{target.DeclaringType}::{target.Name}] to [{target.DeclaringType}::{newName}]", diskOnly: true);
                reference.Name = newName;
            }
        }
    }

    private static bool IsSerializedField(FieldDefinition field)
    {
        // DO NOT RENAME SERIALIZED FIELDS, IT BREAKS UNITY
        return field.CustomAttributes.Select(s => s.Constructor?.DeclaringType?.FullName)
            .Contains("UnityEngine.SerializeField");
    }
}