using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Utils;

namespace AssemblyLib.AutoMatcher.Filters;

public class GeneralFilters
{
    private static readonly List<string> TypesToMatch = DataProvider.Settings.TypeNamesToMatch;
    
    public bool Filter(TypeDefinition target, TypeDefinition candidate, GenericParams parms)
    {
        if (target.IsPublic != candidate.IsPublic) return false;
        if (target.IsAbstract != candidate.IsAbstract) return false;
        if (target.IsInterface != candidate.IsInterface) return false;
        if (target.IsEnum != candidate.IsEnum) return false;
        if (target.IsValueType != candidate.IsValueType) return false;
        if (target.GenericParameters.Count != candidate.GenericParameters.Count) return false;
        if (target.IsNested != candidate.IsNested) return false;
        if (target.IsSealed != candidate.IsSealed) return false;
        if (target.GenericParameters.Count != candidate.GenericParameters.Count) return false;
		
        parms.IsPublic = target.IsPublic;
        parms.IsAbstract = target.IsAbstract;
        parms.IsInterface = target.IsInterface;
        parms.IsEnum = target.IsEnum;
        parms.HasGenericParameters = target.GenericParameters.Any();
        parms.IsSealed = target.IsSealed;
        parms.HasAttribute = target.CustomAttributes.Any();
        parms.IsDerived = target.BaseType != null && target.BaseType.Name != "Object";

        if ((bool)parms.IsDerived && !TypesToMatch.Any(t => target.Name!.StartsWith(t)))
        {
            parms.MatchBaseClass = target.BaseType?.Name;
        }
		
        return true;
    }
}