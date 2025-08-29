using AsmResolver.DotNet;
using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.Filters;

[Injectable(TypePriority = 0)]
public sealed class GenericFilters : IRemapFilter
{
    public bool Filter(
        IEnumerable<TypeDefinition> types,
        RemapModel remapModel,
        out IEnumerable<TypeDefinition> filteredTypes
    )
    {
        var genericParams = remapModel.SearchParams.GenericParams;

        types = types.Where(t => t.IsPublic == genericParams.IsPublic);
        if (!types.Any())
        {
            remapModel.FailureReasons.Add("No remaining candidates after filtering by visibility (public/private)");
            filteredTypes = types;
            return false;
        }

        types = types.Where(t => t.IsAbstract == genericParams.IsAbstract);
        if (!types.Any())
        {
            remapModel.FailureReasons.Add("No remaining candidates after filtering by abstract classes");
            filteredTypes = types;
            return false;
        }

        types = types.Where(t => t.IsSealed == genericParams.IsSealed);
        if (!types.Any())
        {
            remapModel.FailureReasons.Add("No remaining candidates after filtering by sealed classes");
            filteredTypes = types;
            return false;
        }

        types = types.Where(t => t.IsInterface == genericParams.IsInterface);
        if (!types.Any())
        {
            remapModel.FailureReasons.Add("No remaining candidates after filtering by interfaces");
            filteredTypes = types;
            return false;
        }

        types = types.Where(t => t.IsEnum == genericParams.IsEnum);
        if (!types.Any())
        {
            remapModel.FailureReasons.Add("No remaining candidates after filtering by enums");
            filteredTypes = types;
            return false;
        }

        types = types.Where(t => t.GenericParameters.Any() == genericParams.HasGenericParameters);
        if (!types.Any())
        {
            remapModel.FailureReasons.Add("No remaining candidates after filtering by generic parameters");
            filteredTypes = types;
            return false;
        }

        types = FilterAttributes(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.FailureReasons.Add("No remaining candidates after filtering by attributes");
            filteredTypes = types;
            return false;
        }

        types = FilterDerived(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.FailureReasons.Add("No remaining candidates after filtering by derived classes");
            filteredTypes = types;
            return false;
        }

        filteredTypes = types;
        return true;
    }

    private static IEnumerable<TypeDefinition> FilterAttributes(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        return parms.GenericParams.HasAttribute is not null
            ? types.Where(t => t.CustomAttributes.Any() == parms.GenericParams.HasAttribute)
            : types;
    }

    private static IEnumerable<TypeDefinition> FilterDerived(IEnumerable<TypeDefinition> types, SearchParams parms)
    {
        switch (parms.GenericParams.IsDerived)
        {
            case true:
            {
                types = types.Where(t => t.BaseType?.FullName != "System.Object");

                if (parms.GenericParams.MatchBaseClass is not null and not "")
                {
                    types = types.Where(t => t.BaseType?.Name == parms.GenericParams.MatchBaseClass);
                }

                break;
            }

            // Interfaces don't derive from anything
            case false when parms.GenericParams.IsInterface:
                break;

            case false:
                types = types.Where(t => t.BaseType?.FullName == "System.Object");
                break;
        }

        return types;
    }
}
