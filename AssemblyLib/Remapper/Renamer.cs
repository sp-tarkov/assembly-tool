using System.Reflection;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
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
    private readonly IEnumerable<MethodDefinition> _allMethods = types
        .Select(type => type.Methods)
        .SelectMany(methods => methods);
    
    public void RenamePublicizedFieldAndUpdateMemberRefs(FieldDefinition fieldDef, bool isProtected)
    {
        var origName = fieldDef.Name?.ToString();
        
        var newName = string.Empty;
        
        if (origName is null || origName.Length < 3) return;

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

        var methodsToCheck = isProtected
            ? _allMethods
            : fieldDef.DeclaringType?.Methods;
        
        UpdateMemberReferences(fieldDef, newUtf8Name, origName, methodsToCheck!);
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

        //FixMethods(remap);
        
        remap.TypePrimeCandidate.Name = new Utf8String(remap.NewTypeName);
    }

    private void FixMethods(RemapModel remap)
    {
        // TODO: This shit is broken, Have I ever worked?
        // The purpose of this is to demangle interface appended method names
        foreach (var type in types)
        {
            var allMethodNames = type.Methods
                .Select(s => s.Name).ToList();

            // TODO: This is stupid. Past me is an asshole.
            var methodsWithInterfaces = 
                (from method in type.Methods 
                where method.Name.StartsWith(remap.TypePrimeCandidate!.Name)
                select method).ToList();

            foreach (var method in methodsWithInterfaces.ToArray())
            {
                var name = method.Name!.ToString().Split(".");
                
                // TODO: Again with the .ToString() ...
                if (allMethodNames.Count(n => n is not null && n.ToString().EndsWith(name[1])) > 1)
                    continue;
                
                // TODO: Implicit conversion
                method.Name =  method.Name.ToString().Split(".")[1];
            }
        }
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
                
                var methodsToCheck = field.IsPrivate
                    ? field.DeclaringType?.Methods
                    : _allMethods;
                
                UpdateMemberReferences(field, newTypeName, oldName!, methodsToCheck!);
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
        Utf8String newName,
        Utf8String oldName,
        IEnumerable<MethodDefinition> methods)
    {
        // TODO: THIS IS FUCKING SKIPPING VALUE TYPES, WHY. JUST WHY
        
        foreach (var method in methods)
        {
            if (method.CilMethodBody is null) continue;
                
            foreach (var instruction in method.CilMethodBody.Instructions)
            {
                if (instruction.Operand is not MemberReference memberFieldRef ||
                    memberFieldRef.Resolve() != target) continue;
                
                Logger.Log($"Updating Field Reference to [{target.DeclaringType}::{target.Name}] in Method [{method.DeclaringType}::{method.Name}]" +
                           $" to [{method.DeclaringType}::{newName}]", diskOnly: true);
                
                memberFieldRef.Name = newName;
            }
        }
    }
}