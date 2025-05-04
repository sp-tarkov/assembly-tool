using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Utils;

namespace AssemblyLib.AutoMatcher.Filters;

public class MethodFilters
{
    private static readonly List<string> MethodsToIgnore = DataProvider.Settings.MethodNamesToIgnore;
    
    public bool Filter(TypeDefinition target, TypeDefinition candidate, MethodParams methods)
    {
        // Target has no methods and type has no methods
        if (!target.Methods.Any() && !candidate.Methods.Any())
        {
            methods.MethodCount = 0;
            return true;
        }
		
        // Target has no methods but type has methods
        if (!target.Methods.Any() && candidate.Methods.Any()) return false;
		
        // Target has methods but type has no methods
        if (target.Methods.Any() && !candidate.Methods.Any()) return false;
		
        // Target has a different number of methods
        if (target.Methods.Count != candidate.Methods.Count) return false;
		
        var commonMethods = target.Methods
            .Where(m => !m.IsConstructor && m is { IsGetMethod: false, IsSetMethod: false })
            .Select(s => s.Name)
            .Intersect(candidate.Methods
                .Where(m => !m.IsConstructor && m is { IsGetMethod: false, IsSetMethod: false })
                .Select(s => s.Name));
		
        // Methods in target that are not in candidate
        var includeMethods = GetFilteredMethodNamesInType(target)
            .Except(GetFilteredMethodNamesInType(candidate));
		
        // Methods in candidate that are not in target
        var excludeMethods = GetFilteredMethodNamesInType(candidate)
            .Except(GetFilteredMethodNamesInType(target));
		
        methods.IncludeMethods.UnionWith(includeMethods);
        methods.ExcludeMethods.UnionWith(excludeMethods);
		
        methods.MethodCount = target.Methods
            .Count(m => 
                m is { IsConstructor: false, IsGetMethod: false, IsSetMethod: false, IsSpecialName: false });

        if (target.Methods.Any(m => m is { IsConstructor: true, Parameters.Count: > 0 }))
        {
            methods.ConstructorParameterCount = target.Methods.First(m => 
                    m is { IsConstructor: true, Parameters.Count: > 0 })
                .Parameters.Count;
        }
		
        // True if we have common methods, or all methods are constructors
        return commonMethods.Any() || target.Methods.All(m => m.IsConstructor);
    }
    
    private static IEnumerable<string> GetFilteredMethodNamesInType(TypeDefinition type)
    {
        return type.Methods
            .Where(m => m is { IsConstructor: false, IsGetMethod: false, IsSetMethod: false })
            // Don't match de-obfuscator given method names
            .Where(m => !MethodsToIgnore.Any(mi => 
                m.Name!.StartsWith(mi) || m.Name!.Contains('.')))
            .Select(s => s.Name!.ToString());
    }
}