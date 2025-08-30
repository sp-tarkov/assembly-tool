using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.AutoMatcher.Filters;

[Injectable]
public class EventFilters : AbstractAutoMatchFilter
{
    public override bool Filter(TypeDefinition target, TypeDefinition candidate, SearchParams searchParams)
    {
        // Target has no events but type has events
        if (!target.Events.Any() && candidate.Events.Any())
        {
            searchParams.Events.EventCount = 0;
            return LogFailure(
                $"`{candidate.FullName}` filtered out during EventFilters: Target has no events but candidate has events"
            );
        }

        // Target has events but type has no events
        if (target.Events.Any() && !candidate.Events.Any())
        {
            return LogFailure(
                $"`{candidate.FullName}` filtered out during EventFilters: Candidate has events but target has no events"
            );
        }

        // Target has a different number of events
        if (target.Events.Count != candidate.Events.Count)
        {
            return LogFailure(
                $"`{candidate.FullName}` filtered out during EventFilters: Target has a different number of events than the candidate"
            );
        }

        var commonEvents = target.Events.Select(s => s.Name).Intersect(candidate.Events.Select(s => s.Name));

        var includeEvents = target
            .Events.Select(s => s.Name!.ToString())
            .Except(candidate.Events.Select(s => s.Name!.ToString()));

        var excludeEvents = candidate
            .Events.Select(s => s.Name!.ToString())
            .Except(target.Events.Select(s => s.Name!.ToString()));

        searchParams.Events.IncludeEvents.UnionWith(includeEvents);
        searchParams.Events.ExcludeEvents.UnionWith(excludeEvents);
        searchParams.Events.EventCount = target.Events.Count;

        return commonEvents.Any()
            || target.Events.Count == 0
            || LogFailure(
                $"`{candidate.FullName}` filtered out during EventFilters: Target has no common events with candidate"
            );
    }
}
