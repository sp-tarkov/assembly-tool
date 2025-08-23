using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Exceptions;
using AssemblyLib.Models.Interfaces;
using AssemblyLib.Utils;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.AutoMatcher.Filters;

[Injectable]
public class GeneralFilters(
    DataProvider dataProvider
    ) : AbstractAutoMatchFilter
{
    public override bool Filter(TypeDefinition target, TypeDefinition candidate, IFilterParams filterParams)
    {
        if (filterParams is not GenericParams genericParams)
        {
            throw new FilterException("FilterParams in GeneralFilters is not GenericParams or is null");
        }
        
        if (target.IsPublic != candidate.IsPublic) return false;
        if (target.IsAbstract != candidate.IsAbstract) return false;
        if (target.IsInterface != candidate.IsInterface) return false;
        if (target.IsEnum != candidate.IsEnum) return false;
        if (target.IsValueType != candidate.IsValueType) return false;
        if (target.GenericParameters.Count != candidate.GenericParameters.Count) return false;
        if (target.IsNested != candidate.IsNested) return false;
        if (target.IsSealed != candidate.IsSealed) return false;
        if (target.GenericParameters.Count != candidate.GenericParameters.Count) return false;
		
        genericParams.IsPublic = target.IsPublic;
        genericParams.IsAbstract = target.IsAbstract;
        genericParams.IsInterface = target.IsInterface;
        genericParams.IsEnum = target.IsEnum;
        genericParams.HasGenericParameters = target.GenericParameters.Any();
        genericParams.IsSealed = target.IsSealed;
        genericParams.HasAttribute = target.CustomAttributes.Any();
        genericParams.IsDerived = target.BaseType != null && target.BaseType.Name != "Object";

        if ((bool)genericParams.IsDerived && !dataProvider.Settings.TypeNamesToMatch
                .Any(t => target.Name!.StartsWith(t)))
        {
            genericParams.MatchBaseClass = target.BaseType?.Name;
        }
		
        return true;
    }
}