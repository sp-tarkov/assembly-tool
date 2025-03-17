using AssemblyLib.Application;
using AssemblyLib.Models;
using AssemblyLib.Utils;
using dnlib.DotNet;

namespace AssemblyLib.ReMapper;

internal sealed class Renamer(List<TypeDef> types, Statistics stats) 
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
        
        await Logger.DrawProgressBar(renameTasks, "Renaming");
    }

    private void RenameFromRemap(RemapModel remap)
    {
        // Rename all fields and properties first
        RenameAllFields(
            remap.TypePrimeCandidate!.Name.String,
            remap.NewTypeName);

        RenameAllProperties(
            remap.TypePrimeCandidate!.Name.String,
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
                where method.Name.StartsWith(remap.TypePrimeCandidate!.Name.String)
                select method).ToList();

            foreach (var method in methodsWithInterfaces.ToArray())
            {
                var name = method.Name.String.Split(".");
                
                if (allMethodNames.Count(n => n.EndsWith(name[1])) > 1)
                    continue;
                
                method.Name =  method.Name.String.Split(".")[1];
            }
        }
    }
    
    private void RenameAllFields(string oldTypeName, string newTypeName)
    {
        foreach (var type in types)
        {
            var fields = type.Fields
                .Where(field => field.Name.IsFieldOrPropNameInList(TokensToMatch));
            
            var fieldCount = 0;
            foreach (var field in fields)
            {
                if (field.FieldType.TypeName != oldTypeName) continue;
                
                var newFieldName = GetNewFieldName(newTypeName, fieldCount);

                // Dont need to do extra work
                if (field.Name == newFieldName) { continue; }
                    
                var oldName = field.Name.ToString();

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

    private void RenameFieldMemberRefsGlobal(FieldDef newDef, string oldName)
    {
        foreach (var type in types)
        {
            RenameFieldMemberRefsLocal(type, newDef, oldName);
        }
    }

    private static void RenameFieldMemberRefsLocal(TypeDef type, FieldDef newDef, string oldName)
    {
        foreach (var method in type.Methods)
        {
            if (!method.HasBody) continue;

            foreach (var instr in method.Body.Instructions)
            {
                if (instr.Operand is MemberRef memRef && memRef.Name == oldName)
                {
                    memRef.Name = newDef.Name;
                }
            }
        }
    }
    
    private void RenameAllProperties(string oldTypeName, string newTypeName)
    {
        foreach (var type in types)
        {
            var properties = type.Properties
                .Where(prop => prop.Name.IsFieldOrPropNameInList(TokensToMatch));
            
            var propertyCount = 0;
            foreach (var property in properties)
            {
                if (property.PropertySig.RetType.TypeName != oldTypeName) continue;
                
                var newPropertyName = GetNewPropertyName(newTypeName, propertyCount);

                // Dont need to do extra work
                if (property.Name == newPropertyName) continue; 
                    
                property.Name = newPropertyName;

                propertyCount++;
            }
        }
    }

    private UTF8String GetNewFieldName(string newName, int fieldCount = 0)
    {
        var newFieldCount = fieldCount > 0 ? $"_{fieldCount}" : string.Empty;

        stats.FieldRenamedCount++;
        return new UTF8String($"{char.ToLower(newName[0])}{newName[1..]}{newFieldCount}");
    }

    private UTF8String GetNewPropertyName(string newName, int propertyCount = 0)
    {
        stats.PropertyRenamedCount++;
        
        return new UTF8String(propertyCount > 0 ? $"{newName}_{propertyCount}" : newName);
    }
}