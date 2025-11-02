using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.AutoMatcher.Filters;

[Injectable]
public class PropertyFilters : AbstractAutoMatchFilter
{
    public override string FilterName
    {
        get { return "PropertyFilters"; }
    }

    public override bool Filter(TypeDefinition target, TypeDefinition candidate, SearchParams searchParams)
    {
        // Both target and candidate don't have properties
        if (!target.Properties.Any() && !candidate.Properties.Any())
        {
            searchParams.Properties.PropertyCount = 0;
            return true;
        }

        // Target has props but type has no props
        if (target.Properties.Any() && !candidate.Properties.Any())
        {
            return LogFailure(
                $"`{candidate.FullName}` filtered out during PropertyFilters: Target has properties and candidate has no properties"
            );
        }

        // Target has a different number of props
        if (target.Properties.Count != candidate.Properties.Count)
        {
            return LogFailure(
                $"`{candidate.FullName}` filtered out during PropertyFilters: Candidate has a different number of properties"
            );
        }

        var commonProps = target.Properties.Select(s => s.Name).Intersect(candidate.Properties.Select(s => s.Name));

        // Props in target that are not in candidate
        var includeProps = target
            .Properties.Select(s => s.Name!.ToString())
            .Except(candidate.Properties.Select(s => s.Name!.ToString()));

        // Props in candidate that are not in target
        var excludeProps = candidate
            .Properties.Select(s => s.Name!.ToString())
            .Except(target.Properties.Select(s => s.Name!.ToString()));

        searchParams.Properties.IncludeProperties.UnionWith(includeProps);
        searchParams.Properties.ExcludeProperties.UnionWith(excludeProps);

        searchParams.Properties.PropertyCount = target.Properties.Count;

        // Returns true if there are common props so we don't filter it out, or log a failure and return false
        return commonProps.Any()
            || LogFailure(
                $"`{candidate.FullName}` filtered out during PropertyFilters: Candidate has no common properties with target"
            );
    }
}
