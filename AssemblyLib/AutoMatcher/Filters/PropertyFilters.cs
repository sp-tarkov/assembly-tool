using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Exceptions;
using AssemblyLib.Models.Interfaces;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.AutoMatcher.Filters;

[Injectable]
public class PropertyFilters : AbstractAutoMatchFilter
{
    public override bool Filter(TypeDefinition target, TypeDefinition candidate, IFilterParams filterParams)
    {
        if (filterParams is not PropertyParams propertyParams)
        {
            throw new FilterException("FilterParams in PropertyFilters is not PropertyParams or is null");
        }
        
        // Both target and candidate don't have properties
        if (!target.Properties.Any() && !candidate.Properties.Any())
        {
            propertyParams.PropertyCount = 0;
            return true;
        }
		
        // Target has props but type has no props
        if (target.Properties.Any() && !candidate.Properties.Any()) return false;
		
        // Target has a different number of props
        if (target.Properties.Count != candidate.Properties.Count) return false;
		
        var commonProps = target.Properties
            .Select(s => s.Name)
            .Intersect(candidate.Properties.Select(s => s.Name));
		
        // Props in target that are not in candidate
        var includeProps = target.Properties
            .Select(s => s.Name!.ToString())
            .Except(candidate.Properties.Select(s => s.Name!.ToString()));
		
        // Props in candidate that are not in target
        var excludeProps = candidate.Properties
            .Select(s => s.Name!.ToString())
            .Except(target.Properties.Select(s => s.Name!.ToString()));
		
        propertyParams.IncludeProperties.UnionWith(includeProps);
        propertyParams.ExcludeProperties.UnionWith(excludeProps);
		
        propertyParams.PropertyCount = target.Properties.Count;
		
        return commonProps.Any();
    }
}