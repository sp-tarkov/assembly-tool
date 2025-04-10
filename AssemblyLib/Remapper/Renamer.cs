using System.Reflection.Metadata;
using AsmResolver;
using AssemblyLib.Application;
using AssemblyLib.Models;
using AssemblyLib.Utils;
using FieldDefinition = AsmResolver.DotNet.FieldDefinition;
using MemberReference = AsmResolver.DotNet.MemberReference;
using TypeDefinition = AsmResolver.DotNet.TypeDefinition;

namespace AssemblyLib.ReMapper;

internal sealed class Renamer(List<TypeDefinition> types, Statistics stats) 
    : IComponent
{
    public void RenamePublicizedFieldAndUpdateMemberRefs(FieldDefinition fieldDef, bool isProtected)
    {
        var origName = fieldDef.Name?.ToString();
        var newName = string.Empty;
        
        if (origName is null || origName.Length == 0) return;

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
        
        //Logger.Log($"Changing field {origName} to {newName}");
        
        fieldDef.Name = new Utf8String(newName);

        if (isProtected)
        {
            RenameFieldMemberRefsGlobal(fieldDef, origName);
        }
        else
        {
            RenameFieldMemberRefsLocal(fieldDef.DeclaringType!, fieldDef, origName);
        }
    }
    
    public void RenameRemap(RemapModel remap)
    {
        // Rename all fields and properties first
        
        // TODO: Passing strings as Utf8String, fix the model
        RenameAllFields(
            remap.TypePrimeCandidate!.Name!,
            remap.NewTypeName);
        
        RenameAllProperties(
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
    
    private void RenameAllFields(Utf8String oldTypeName, Utf8String newTypeName)
    {
        foreach (var type in types)
        {
            var fields = type.Fields
                .Where(field => field.Name!.IsObfuscatedName());
            
            var fieldCount = 0;
            foreach (var field in fields)
            {
                if (field.Signature?.FieldType.Name != oldTypeName) continue;
                
                var newFieldName = GetNewFieldName(newTypeName, fieldCount);

                // Dont need to do extra work
                if (field.Name == newFieldName) { continue; }
                    
                var oldName = field.Name;

                field.Name = newFieldName;
                
                if (field.IsPrivate)
                {
                    RenameFieldMemberRefsLocal(type, field, oldName!);
                }
                else
                {
                    RenameFieldMemberRefsGlobal(field, oldName!);
                }
                

                fieldCount++;
            }
        }
    }
    
    private void RenameFieldMemberRefsGlobal(FieldDefinition fieldDef, Utf8String oldName)
    {
        foreach (var type in types)
        {
            RenameFieldMemberRefsLocal(type, fieldDef, oldName);
        }
    }

    private static void RenameFieldMemberRefsLocal(TypeDefinition type, FieldDefinition fieldDef, Utf8String oldName)
    {
        foreach (var method in type.Methods)
        {
            if (!method.HasMethodBody) continue;

            foreach (var instr in method.CilMethodBody!.Instructions)
            {
                if (instr.Operand is MemberReference memRef && memRef.Name == oldName)
                {
                    memRef.Name = fieldDef.Name;
                }
            }
        }
    }
    
    private void RenameAllProperties(Utf8String oldTypeName, Utf8String newTypeName)
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
                    
                property.Name = newPropertyName;

                propertyCount++;
            }
        }
    }

    private Utf8String GetNewFieldName(string newName, int fieldCount = 0)
    {
        var newFieldCount = fieldCount > 0 ? $"_{fieldCount}" : string.Empty;

        // TODO: This needs to take visibility flags into account
        
        stats.FieldRenamedCount++;
        return new Utf8String($"{char.ToLower(newName[0])}{newName[1..]}{newFieldCount}");
    }

    private Utf8String GetNewPropertyName(string newName, int propertyCount = 0)
    {
        stats.PropertyRenamedCount++;
        
        return new Utf8String(propertyCount > 0 ? $"{newName}_{propertyCount}" : newName);
    }
}