using System.Reflection.Metadata;
using AsmResolver;
using AssemblyLib.Application;
using AssemblyLib.Models;
using AssemblyLib.Utils;
using FieldDefinition = AsmResolver.DotNet.FieldDefinition;
using TypeDefinition = AsmResolver.DotNet.TypeDefinition;

namespace AssemblyLib.ReMapper;

internal sealed class Renamer(List<TypeDefinition> types, Statistics stats) 
    : IComponent
{
    private static List<string> TokensToMatch => DataProvider.Settings!.TypeNamesToMatch;

    public async Task StartRenameProcess()
    {
        await StartRemapTask();
    }
    
    private async Task StartRemapTask()
    {
        var renameTasks = new List<Task>(DataProvider.Remaps.Count);
        foreach (var remap in DataProvider.Remaps)
        {
            renameTasks.Add(
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        RenameFromRemap(remap);
                    }
                    catch (Exception ex)
                    {
                        Logger.QueueTaskException($"Exception in task: {ex.Message}");
                    }
                })
            );
        }

        if (DataProvider.Settings.DebugLogging)
        {
            await Task.WhenAll(renameTasks.ToArray());
            return;
        }
        
        await Logger.DrawProgressBar(renameTasks, "Renaming");
    }

    private void RenameFromRemap(RemapModel remap)
    {
        // Rename all fields and properties first
        RenameAllFields(
            remap.TypePrimeCandidate!.Name!,
            remap.NewTypeName);

        RenameAllProperties(
            remap.TypePrimeCandidate!.Name!,
            remap.NewTypeName);

        FixMethods(remap);
        
        remap.TypePrimeCandidate.Name = remap.NewTypeName;
    }

    private void FixMethods(RemapModel remap)
    {
        foreach (var type in types)
        {
            var allMethodNames = type.Methods
                .Select(s => s.Name).ToList();

            var methodsWithInterfaces = 
                (from method in type.Methods
                where method.Name.ToString().StartsWith(remap.TypePrimeCandidate!.Name)
                select method).ToList();

            foreach (var method in methodsWithInterfaces.ToArray())
            {
                var name = method.Name!.ToString().Split(".");
                
                if (allMethodNames.Count(n => n is not null && n.ToString().EndsWith(name[1])) > 1)
                    continue;
                
                method.Name =  method.Name.ToString().Split(".")[1];
            }
        }
    }
    
    private void RenameAllFields(Utf8String oldTypeName, Utf8String newTypeName)
    {
        foreach (var type in types)
        {
            var fields = type.Fields
                .Where(field => field.Name!.ToString().IsFieldOrPropNameInList(TokensToMatch));
            
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
                    RenameFieldMemberRefsLocal(type, field, oldName);
                }
                else
                {
                    RenameFieldMemberRefsGlobal(field, oldName);
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

    private static void RenameFieldMemberRefsLocal(TypeDefinition type, FieldDefinition fieldDef, string oldName)
    {
        foreach (var method in type.Methods)
        {
            if (!method.HasMethodBody) continue;

            foreach (var instr in method.CilMethodBody!.Instructions)
            {
                if (instr.Operand is FieldDefinition memRef && memRef.Name == oldName)
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
                .Where(prop => prop.Name!.ToString().IsFieldOrPropNameInList(TokensToMatch));
            
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

        stats.FieldRenamedCount++;
        return new Utf8String($"{char.ToLower(newName[0])}{newName[1..]}{newFieldCount}");
    }

    private Utf8String GetNewPropertyName(string newName, int propertyCount = 0)
    {
        stats.PropertyRenamedCount++;
        
        return new Utf8String(propertyCount > 0 ? $"{newName}_{propertyCount}" : newName);
    }
}