using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Enums;
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
            remapModel.NoMatchReasons.Add(ENoMatchReason.IsPublic);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = types;
            return false;
        }

        types = types.Where(t => t.IsAbstract == genericParams.IsAbstract);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.IsAbstract);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = types;
            return false;
        }

        types = types.Where(t => t.IsSealed == genericParams.IsSealed);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.IsSealed);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = types;
            return false;
        }

        types = types.Where(t => t.IsInterface == genericParams.IsInterface);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.IsInterface);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = types;
            return false;
        }

        types = types.Where(t => t.IsEnum == genericParams.IsEnum);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.IsEnum);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = types;
            return false;
        }

        types = types.Where(t => t.GenericParameters.Any() == genericParams.HasGenericParameters);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.HasGenericParameters);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = types;
            return false;
        }

        types = FilterAttributes(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.HasAttribute);
            remapModel.TypeCandidates.UnionWith(types);
            filteredTypes = types;
            return false;
        }

        types = FilterDerived(types, remapModel.SearchParams);
        if (!types.Any())
        {
            remapModel.NoMatchReasons.Add(ENoMatchReason.HasAttribute);
            remapModel.TypeCandidates.UnionWith(types);
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
