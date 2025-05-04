using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Utils;

namespace AssemblyLib.AutoMatcher.Filters;

public class FieldFilters
{
    private static readonly List<string> FieldsToIgnore = DataProvider.Settings.FieldNamesToIgnore;
    
    public bool Filter(TypeDefinition target, TypeDefinition candidate, FieldParams fields)
    {
        // Target has no fields and type has no fields
        if (!target.Fields.Any() && !candidate.Fields.Any())
        {
            fields.FieldCount = 0;
            return true;
        }
		
        // Target has fields but type has no fields
        if (target.Fields.Any() && !candidate.Fields.Any()) return false;
		
        // Target has a different number of fields
        if (target.Fields.Count != candidate.Fields.Count) return false;

        var targetFields = GetFilteredFieldNamesInType(target)
            .ToArray();
		
        var candidateFields = GetFilteredFieldNamesInType(candidate)
            .ToArray();
		
        var commonFields = targetFields
            .Intersect(candidateFields);
		
        // Fields in target that are not in candidate
        var includeFields = targetFields
            .Except(candidateFields);
		
        // Fields in candidate that are not in target
        var excludeFields = candidateFields
            .Except(targetFields);
		
        fields.IncludeFields.UnionWith(includeFields);
        fields.ExcludeFields.UnionWith(excludeFields);
		
        fields.FieldCount = target.Fields.Count;
		
        return commonFields.Any();
    }
    
    private static IEnumerable<string> GetFilteredFieldNamesInType(TypeDefinition type)
    {
        return type.Fields
            // Don't match de-obfuscator given method names
            .Where(m => !FieldsToIgnore.Any(mi => m.Name!.StartsWith(mi)))
            .Select(s => s.Name!.ToString());
    }
}