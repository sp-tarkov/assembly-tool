using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.AutoMatcher.Filters;

[Injectable]
public class EventFilters
{
    public bool Filter(TypeDefinition target, TypeDefinition candidate, EventParams events)
    {
        // Target has no events but type has events
        if (!target.Events.Any() && candidate.Events.Any())
        {
            events.EventCount = 0;
            return false;
        }
		
        // Target has events but type has no events
        if (target.Events.Any() && !candidate.Events.Any()) return false;
		
        // Target has a different number of events
        if (target.Events.Count != candidate.Events.Count) return false;
		
        var commonEvents = target.Events
            .Select(s => s.Name)
            .Intersect(candidate.Events.Select(s => s.Name));
		
        var includeEvents = target.Events
            .Select(s => s.Name!.ToString())
            .Except(candidate.Events.Select(s => s.Name!.ToString()))
            .ToHashSet();
		
        var excludeEvents = candidate.Events
            .Select(s => s.Name!.ToString())
            .Except(target.Events.Select(s => s.Name!.ToString()))
            .ToHashSet();
        
        events.IncludeEvents.UnionWith(includeEvents);
        events.ExcludeEvents.UnionWith(excludeEvents);
        events.EventCount = target.NestedTypes.Count;
		
        return commonEvents.Any() || target.Events.Count == 0;
    }
}