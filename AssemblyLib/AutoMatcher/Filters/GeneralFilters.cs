using AsmResolver.DotNet;
using AssemblyLib.Extensions;
using AssemblyLib.Models;
using AssemblyLib.Utils;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.AutoMatcher.Filters;

[Injectable]
public class GeneralFilters(DataProvider dataProvider) : AbstractAutoMatchFilter
{
    public override bool Filter(TypeDefinition target, TypeDefinition candidate, SearchParams searchParams)
    {
        if (target.IsPublic && !candidate.IsPublic)
        {
            return LogFailure(
                $"`{candidate.FullName}` filtered out during GeneralFilters: Target is public but candidate is not"
            );
        }

        if (target.IsAbstract && !candidate.IsAbstract)
        {
            return LogFailure(
                $"`{candidate.FullName}` filtered out during GeneralFilters: Target is abstract but candidate is not"
            );
        }

        if (target.IsInterface && !candidate.IsInterface)
        {
            return LogFailure(
                $"`{candidate.FullName}` filtered out during GeneralFilters: Target is an interface but candidate is not"
            );
        }

        if (target.IsEnum && !candidate.IsEnum)
        {
            return LogFailure(
                $"`{candidate.FullName}` filtered out during GeneralFilters: Target is an enum but candidate is not"
            );
        }

        if (target.IsValueType && !candidate.IsValueType)
        {
            return LogFailure(
                $"`{candidate.FullName}` filtered out during GeneralFilters: Target is a value type but candidate is not"
            );
        }

        if (target.GenericParameters.Count != candidate.GenericParameters.Count)
        {
            return LogFailure(
                $"`{candidate.FullName}` filtered out during GeneralFilters: Candidate has a different number of generic parameters"
            );
        }

        if (target.IsNested && !candidate.IsNested)
        {
            return LogFailure(
                $"`{candidate.FullName}` filtered out during GeneralFilters: Target is nested but candidate is not"
            );
        }

        if (target.IsSealed && !candidate.IsSealed)
        {
            return LogFailure(
                $"`{candidate.FullName}` filtered out during GeneralFilters: Target is sealed but candidate is not"
            );
        }

        searchParams.GenericParams.IsPublic = target.IsPublic;
        searchParams.GenericParams.IsAbstract = target.IsAbstract;
        searchParams.GenericParams.IsInterface = target.IsInterface;
        searchParams.GenericParams.IsEnum = target.IsEnum;
        searchParams.GenericParams.HasGenericParameters = target.GenericParameters.Any();
        searchParams.GenericParams.IsSealed = target.IsSealed;
        searchParams.GenericParams.HasAttribute = target.CustomAttributes.Any();

        switch (target.IsValueType)
        {
            // Structs are never derived
            case false:
                searchParams.GenericParams.IsDerived = target.BaseType != null && target.BaseType.Name != "Object";
                break;
            case true when !target.IsEnum:
                searchParams.GenericParams.IsStruct = true;
                break;
        }

        if (
            (bool)searchParams.GenericParams.IsDerived
            && !dataProvider.Settings.TypeNamesToMatch.Any(t => target.Name!.StartsWith(t))
        )
        {
            searchParams.GenericParams.MatchBaseClass = target.BaseType?.Name;
        }

        return true;
    }
}
