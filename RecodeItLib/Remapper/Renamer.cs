using dnlib.DotNet;
using ReCodeItLib.Models;
using ReCodeItLib.Utils;

namespace ReCodeItLib.ReMapper;

internal class Renamer
{
    private static List<string>? TokensToMatch => DataProvider.Settings?.Remapper?.TokensToMatch;

    /// <summary>
    /// Only used by the manual remapper, should probably be removed
    /// </summary>
    /// <param name="module"></param>
    /// <param name="remap"></param>
    /// <param name="direct"></param>
    public void RenameAll(IEnumerable<TypeDef> types, RemapModel remap)
    {
        if (remap.TypePrimeCandidate is null)
        {
            Logger.Log($"Unable to rename {remap.NewTypeName} as TypePrimeCandidate value is null/empty, skipping", ConsoleColor.Red);
            return;
        }
        
        // Rename all fields and properties first
        RenameAllFields(
            remap.TypePrimeCandidate.Name.String,
            remap.NewTypeName,
            types);

        RenameAllProperties(
            remap!.TypePrimeCandidate!.Name.String,
            remap.NewTypeName,
            types);

        FixMethods(types, remap);
        RenameType(types, remap);
    }

    private static void FixMethods(
        IEnumerable<TypeDef> typesToCheck, 
        RemapModel remap)
    {
        foreach (var type in typesToCheck)
        {
            var methods = type.Methods
                .Where(method => method.Name.StartsWith(remap!.TypePrimeCandidate!.Name.String) 
                && type.Methods.Count(m2 => m2.Name.String.EndsWith(method.Name.String.Split(".")[1])) < 2);

            foreach (var method in methods)
            {
                var name = method.Name.String.Split(".");
                
                if (methods.Count(m => m.Name.String.EndsWith(name[1])) > 1)
                    continue;
                
                method.Name = name[1];
            }
        }
    }
    
    /// <summary>
    /// Rename all fields recursively, returns number of fields changed
    /// </summary>
    /// <param name="oldTypeName"></param>
    /// <param name="newTypeName"></param>
    /// <param name="typesToCheck"></param>
    private static void RenameAllFields(

        string oldTypeName,
        string newTypeName,
        IEnumerable<TypeDef> typesToCheck)
    {
        foreach (var type in typesToCheck)
        {
            var fields = type.Fields
                .Where(field => field.Name.IsFieldOrPropNameInList(TokensToMatch!));

            if (!fields.Any()) { continue; }

            int fieldCount = 0;
            foreach (var field in fields)
            {
                if (field.FieldType.TypeName == oldTypeName)
                {
                    var newFieldName = GetNewFieldName(newTypeName, fieldCount);

                    // Dont need to do extra work
                    if (field.Name == newFieldName) { continue; }
                    
                    var oldName = field.Name.ToString();

                    field.Name = newFieldName;
                    
                    UpdateAllTypeFieldMemberRefs(typesToCheck, field, oldName);

                    fieldCount++;
                }
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
    
    /// <summary>
    /// Rename all properties recursively, returns number of fields changed
    /// </summary>
    /// <param name="oldTypeName"></param>
    /// <param name="newTypeName"></param>
    /// <param name="typesToCheck"></param>
    private static void RenameAllProperties(
        string oldTypeName,
        string newTypeName,
        IEnumerable<TypeDef> typesToCheck)
    {
        foreach (var type in typesToCheck)
        {
            var properties = type.Properties
                .Where(prop => prop.Name.IsFieldOrPropNameInList(TokensToMatch!));

            if (!properties.Any()) { continue; }

            int propertyCount = 0;
            foreach (var property in properties)
            {
                if (property.PropertySig.RetType.TypeName == oldTypeName)
                {
                    var newPropertyName = GetNewPropertyName(newTypeName, propertyCount);

                    // Dont need to do extra work
                    if (property.Name == newPropertyName) { continue; }
                    
                    property.Name = new UTF8String(newPropertyName);

                    propertyCount++;
                }
            }
        }
    }

    private static string GetNewFieldName(string NewName, int fieldCount = 0)
    {
        string newFieldCount = fieldCount > 0 ? $"_{fieldCount}" : string.Empty;

        return $"{char.ToLower(NewName[0])}{NewName[1..]}{newFieldCount}";
    }

    private static string GetNewPropertyName(string newName, int propertyCount = 0)
    {
        return propertyCount > 0 ? $"{newName}_{propertyCount}" : newName;
    }

    private static void RenameType(IEnumerable<TypeDef> typesToCheck, RemapModel remap)
    {
        foreach (var type in typesToCheck)
        {
            if (type.HasNestedTypes)
            {
                RenameType(type.NestedTypes, remap!);
            }

            if (remap?.TypePrimeCandidate?.Name is null) { continue; }

            if (remap.SearchParams.NestedTypes.IsNested is true &&
                type.IsNested && type.Name == remap.TypePrimeCandidate.Name)
            {
                type.Name = remap.NewTypeName;
            }

            if (type.FullName == remap.TypePrimeCandidate.Name)
            {
                type.Name = remap.NewTypeName;
            }
        }
    }
}