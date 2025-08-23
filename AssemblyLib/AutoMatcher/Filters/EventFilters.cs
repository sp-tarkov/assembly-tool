using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Exceptions;
using AssemblyLib.Models.Interfaces;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.AutoMatcher.Filters;

[Injectable]
public class EventFilters : AbstractAutoMatchFilter
{
    public override bool Filter(TypeDefinition target, TypeDefinition candidate, IFilterParams filterParams)
    {
        if (filterParams is not EventParams eventParams)
        {
            throw new FilterException("FilterParams in EventFilters is not EventParams or is null");
        }
        
        // Target has no events but type has events
        if (!target.Events.Any() && candidate.Events.Any())
        {
            eventParams.EventCount = 0;
            return LogFailure($"`{candidate.FullName}` filtered out during EventFilters: Target has no events but candidate has events");
        }
		
        // Target has events but type has no events
        if (target.Events.Any() && !candidate.Events.Any())
        {
            return LogFailure($"`{candidate.FullName}` filtered out during EventFilters: Candidate has events but target has no events");
        }
		
        // Target has a different number of events
        if (target.Events.Count != candidate.Events.Count)
        {
            return LogFailure($"`{candidate.FullName}` filtered out during EventFilters: Target has a different number of events than the candidate");
        }
		
        var commonEvents = target.Events
            .Select(s => s.Name)
            .Intersect(candidate.Events.Select(s => s.Name));
		
        var includeEvents = target.Events
            .Select(s => s.Name!.ToString())
            .Except(candidate.Events.Select(s => s.Name!.ToString()));
		
        var excludeEvents = candidate.Events
            .Select(s => s.Name!.ToString())
            .Except(target.Events.Select(s => s.Name!.ToString()));
        
        eventParams.IncludeEvents.UnionWith(includeEvents);
        eventParams.ExcludeEvents.UnionWith(excludeEvents);
        eventParams.EventCount = target.NestedTypes.Count;
		
        return commonEvents.Any() || 
               target.Events.Count == 0 || 
               LogFailure($"`{candidate.FullName}` filtered out during EventFilters: Target has no common events with candidate");
    }
}