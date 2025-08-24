using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Enums;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.Filters;

[Injectable]
public sealed class GenericFilters : IRemapFilter
{
    public bool Filter(
        IEnumerable<TypeDefinition> types,
        RemapModel remapModel,
        out List<TypeDefinition>? filteredTypes
    )
    {
        var genericParams = remapModel.SearchParams.GenericParams;

        var internFilteredTypes = types.Where(t => t.IsPublic == genericParams.IsPublic);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.IsPublic);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = internFilteredTypes.Where(t => t.IsAbstract == genericParams.IsAbstract);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.IsAbstract);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = internFilteredTypes.Where(t => t.IsSealed == genericParams.IsSealed);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.IsSealed);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = internFilteredTypes.Where(t => t.IsInterface == genericParams.IsInterface);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.IsInterface);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = internFilteredTypes.Where(t => t.IsEnum == genericParams.IsEnum);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.IsEnum);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = internFilteredTypes.Where(t =>
            t.GenericParameters.Any() == genericParams.HasGenericParameters
        );
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.HasGenericParameters);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = FilterAttributes(internFilteredTypes, remapModel.SearchParams);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.HasAttribute);
            filteredTypes = null;
            return false;
        }

        internFilteredTypes = FilterDerived(internFilteredTypes, remapModel.SearchParams);
        if (!internFilteredTypes.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.HasAttribute);
            filteredTypes = null;
            return false;
        }

        filteredTypes = internFilteredTypes.ToList();
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
        // Filter based on IsDerived or not
        if (parms.GenericParams.IsDerived is true)
        {
            types = types.Where(t => t.BaseType?.Name != "Object");

            if (parms.GenericParams.MatchBaseClass is not null and not "")
            {
                types = types.Where(t => t.BaseType?.Name == parms.GenericParams.MatchBaseClass);
            }
        }
        else if (parms.GenericParams.IsDerived is false)
        {
            types = types.Where(t => t.BaseType?.Name == "Object");
        }

        return types;
    }
}
