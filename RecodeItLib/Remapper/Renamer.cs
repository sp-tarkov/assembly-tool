using dnlib.DotNet;
using ReCodeItLib.Models;
using ReCodeItLib.Utils;

namespace ReCodeItLib.ReMapper;

internal class Renamer(Statistics stats)
{
    private static List<string> TokensToMatch => DataProvider.Settings!.TypeNamesToMatch;
    
    public void RenameAll(IEnumerable<TypeDef> types, RemapModel remap)
    {
        if (remap.TypePrimeCandidate is null) return;
        
        // Rename all fields and properties first
        var typesToCheck = types as TypeDef[] ?? types.ToArray();
        RenameAllFields(
            remap.TypePrimeCandidate.Name.String,
            remap.NewTypeName,
            typesToCheck);

        RenameAllProperties(
            remap.TypePrimeCandidate!.Name.String,
            remap.NewTypeName,
            typesToCheck);

        FixMethods(typesToCheck, remap);
        
        remap.TypePrimeCandidate.Name = remap.NewTypeName;
    }

    private static void FixMethods(
        IEnumerable<TypeDef> typesToCheck, 
        RemapModel remap)
    {
        foreach (var type in typesToCheck)
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
    
    private void RenameAllFields(

        string oldTypeName,
        string newTypeName,
        IEnumerable<TypeDef> typesToCheck)
    {
        var typeDefs = typesToCheck as TypeDef[] ?? typesToCheck.ToArray();
        foreach (var type in typeDefs)
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
                    
                UpdateAllTypeFieldMemberRefs(typeDefs, field, oldName);

                fieldCount++;
            }
        }
    }

    private static void UpdateAllTypeFieldMemberRefs(IEnumerable<TypeDef> typesToCheck, FieldDef newDef, string oldName)
    {
        foreach (var type in typesToCheck)
        {
            UpdateTypeFieldMemberRefs(type, newDef, oldName);
        }
    }

    private static void UpdateTypeFieldMemberRefs(TypeDef type, FieldDef newDef, string oldName)
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
    
    private void RenameAllProperties(
        string oldTypeName,
        string newTypeName,
        IEnumerable<TypeDef> typesToCheck)
    {
        foreach (var type in typesToCheck)
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
        
        return new UTF8String(propertyCount > 0 ? $"{newName}_{propertyCount}" : newName);;
    }
}