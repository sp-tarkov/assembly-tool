using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Exceptions;
using AssemblyLib.Models.Interfaces;
using AssemblyLib.Utils;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.AutoMatcher.Filters;

[Injectable]
public class FieldFilters(
    DataProvider dataProvider
    ) : AbstractAutoMatchFilter
{
    private List<string>? _fieldNamesToIgnore;
    
    public override bool Filter(TypeDefinition target, TypeDefinition candidate, IFilterParams filterParams)
    {
        if (filterParams is not FieldParams fieldParams)
        {
            throw new FilterException("FilterParams in FieldFilters is not FieldParams or is null");
        }
        
        _fieldNamesToIgnore ??= dataProvider.Settings.FieldNamesToIgnore;
        
        // Target has no fields and type has no fields
        if (!target.Fields.Any() && !candidate.Fields.Any())
        {
            fieldParams.FieldCount = 0;
            return true;
        }
		
        // Target has fields but type has no fields
        if (target.Fields.Any() && !candidate.Fields.Any())
        {
            return LogFailure($"`{candidate.FullName}` filtered out during FieldFilters: Target has fields but candidate has no fields");
        }
		
        // Target has a different number of fields
        if (target.Fields.Count != candidate.Fields.Count)
        {
            return LogFailure($"`{candidate.FullName}` filtered out during FieldFilters: Target has a different number of fields than the candidate");
        }

        var targetFields = GetFilteredFieldNamesInType(target);
        var candidateFields = GetFilteredFieldNamesInType(candidate);
		
        var commonFields = targetFields
            .Intersect(candidateFields);
		
        // Fields in target that are not in candidate
        var includeFields = targetFields
            .Except(candidateFields);
		
        // Fields in candidate that are not in target
        var excludeFields = candidateFields
            .Except(targetFields);
		
        fieldParams.IncludeFields.UnionWith(includeFields);
        fieldParams.ExcludeFields.UnionWith(excludeFields);
		
        fieldParams.FieldCount = target.Fields.Count;
		
        return commonFields.Any() ||
               LogFailure($"`{candidate.FullName}` filtered out during FieldFilters: Target has no common fields with candidate");
    }
    
    private string[] GetFilteredFieldNamesInType(TypeDefinition type)
    {
        return type.Fields
            // Don't match de-obfuscator given method names
            .Where(m => !_fieldNamesToIgnore?.Any(mi => m.Name!.StartsWith(mi)) ?? false)
            .Select(s => s.Name!.ToString())
            .ToArray();
    }
}