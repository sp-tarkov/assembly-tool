using AsmResolver;
using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Exceptions;
using AssemblyLib.Models.Interfaces;
using AssemblyLib.Utils;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.AutoMatcher.Filters;

[Injectable]
public class MethodFilters(
    DataProvider dataProvider
    ) : AbstractAutoMatchFilter
{
    private List<string>? _methodsToIgnore;
    
    public override bool Filter(TypeDefinition target, TypeDefinition candidate, SearchParams searchParams)
    {
        _methodsToIgnore ??= dataProvider.Settings.MethodNamesToIgnore;
        
        // Target has no methods and type has no methods
        if (!target.Methods.Any() && !candidate.Methods.Any())
        {
            searchParams.Methods.MethodCount = 0;
            return true;
        }
		
        // Target has no methods but type has methods
        if (!target.Methods.Any() && candidate.Methods.Any())
        {
            return LogFailure($"`{candidate.FullName}` filtered out during MethodFilters: Target has no methods but candidate does");
        }
		
        // Target has methods but type has no methods
        if (target.Methods.Any() && !candidate.Methods.Any())
        {
            return LogFailure($"`{candidate.FullName}` filtered out during MethodFilters: Target has methods but candidate does not");
        }
		
        // Target has a different number of methods
        if (target.Methods.Count != candidate.Methods.Count)
        {
            return LogFailure($"`{candidate.FullName}` filtered out during MethodFilters: Target has a different number of methods");
        }
        
        // Methods in target that are not in candidate
        var includeMethods = GetFilteredMethodNamesInType(target)
            .Except(GetFilteredMethodNamesInType(candidate));
		
        // Methods in candidate that are not in target
        var excludeMethods = GetFilteredMethodNamesInType(candidate)
            .Except(GetFilteredMethodNamesInType(target));
		
        searchParams.Methods.IncludeMethods.UnionWith(includeMethods);
        searchParams.Methods.ExcludeMethods.UnionWith(excludeMethods);
		
        searchParams.Methods.MethodCount = target.Methods
            .Count(m => 
                m is { IsConstructor: false, IsGetMethod: false, IsSetMethod: false, IsSpecialName: false });

        if (target.Methods.Any(m => m is { IsConstructor: true, Parameters.Count: > 0 }))
        {
            searchParams.Methods.ConstructorParameterCount = target.Methods.First(m => 
                    m is { IsConstructor: true, Parameters.Count: > 0 })
                .Parameters.Count;
        }
		
        // True if we have common methods, or all methods are constructors
        return HasCommonMethods(target, candidate) || 
               target.Methods.All(m => m.IsConstructor) || 
               LogFailure($"`{candidate.FullName}` filtered out during MethodFilters: Candidate has no common methods");
    }
    
    /// <summary>
    /// Filter method names to those we can use for matching, do not include interface pre-appended method names,
    /// or any that are de-obfuscator given 
    /// </summary>
    /// <param name="type">Type to clean methods on</param>
    /// <returns>A collection of cleaned method names</returns>
    private IEnumerable<string> GetFilteredMethodNamesInType(TypeDefinition type)
    {
        return type.Methods
            .Where(m => m is { IsConstructor: false, IsGetMethod: false, IsSetMethod: false })
            // Don't match de-obfuscator given method names
            .Where(m => !_methodsToIgnore?.Any(mi => 
                m.Name!.StartsWith(mi) || m.Name!.Contains('.')) ?? false)
            .Select(s => s.Name!.ToString());
    }

    /// <summary>
    /// Produce an intersecting set of methods by name and return if any are common
    /// </summary>
    /// <param name="target">Target type</param>
    /// <param name="candidate">Candidate type</param>
    /// <returns>True if there are common methods</returns>
    private static bool HasCommonMethods(TypeDefinition target, TypeDefinition candidate)
    {
        return target.Methods
                // Get target methods that are not a constructor a get, or set method
            .Where(m => m is { IsConstructor: false, IsGetMethod: false, IsSetMethod: false })
            .Select(s => s.Name)
                // Produce a set of method names that exist in both the target and the candidate
            .Intersect(candidate.Methods
                // Get candidate methods that are not a constructor a get, or set method
                .Where(m => m is { IsConstructor: false, IsGetMethod: false, IsSetMethod: false })
                .Select(s => s.Name))
                // Is there any common methods?
            .Any();
    }
}