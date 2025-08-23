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

        if (target.IsPublic && !candidate.IsPublic)
        {
            return LogFailure($"`{candidate.FullName}` filtered out during GeneralFilters: Target is public but candidate is not");
        }

        if (target.IsAbstract && !candidate.IsAbstract)
        {
            return LogFailure($"`{candidate.FullName}` filtered out during GeneralFilters: Target is abstract but candidate is not");
        }

        if (target.IsInterface && !candidate.IsInterface)
        {
            return LogFailure($"`{candidate.FullName}` filtered out during GeneralFilters: Target is an interface but candidate is not");
        }

        if (target.IsEnum && !candidate.IsEnum)
        {
            return LogFailure($"`{candidate.FullName}` filtered out during GeneralFilters: Target is an enum but candidate is not");
        }

        if (target.IsValueType && !candidate.IsValueType)
        {
            return LogFailure($"`{candidate.FullName}` filtered out during GeneralFilters: Target is a value type but candidate is not");
        }

        if (target.GenericParameters.Count != candidate.GenericParameters.Count)
        {
            return LogFailure($"`{candidate.FullName}` filtered out during GeneralFilters: Candidate has a different number of generic parameters");
        }

        if (target.IsNested && !candidate.IsNested)
        {
            return LogFailure($"`{candidate.FullName}` filtered out during GeneralFilters: Target is nested but candidate is not");
        }

        if (target.IsSealed && !candidate.IsSealed)
        {
            return LogFailure($"`{candidate.FullName}` filtered out during GeneralFilters: Target is sealed but candidate is not");
        }
		
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