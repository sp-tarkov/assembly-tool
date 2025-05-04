using AsmResolver.DotNet;
using AssemblyLib.Models;

namespace AssemblyLib.AutoMatcher.Filters;

public class PropertyFilters
{
    public bool Filter(TypeDefinition target, TypeDefinition candidate, PropertyParams props)
    {
        // Both target and candidate don't have properties
        if (!target.Properties.Any() && !candidate.Properties.Any())
        {
            props.PropertyCount = 0;
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
		
        props.IncludeProperties.UnionWith(includeProps);
        props.ExcludeProperties.UnionWith(excludeProps);
		
        props.PropertyCount = target.Properties.Count;
		
        return commonProps.Any();
    }
}