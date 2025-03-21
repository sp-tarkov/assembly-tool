using AsmResolver.DotNet;
using AssemblyLib.Enums;
using AssemblyLib.Models;
using AssemblyLib.ReMapper.Filters;

namespace AssemblyLib.ReMapper;

public class TypeFilters
{
    public bool DoesTypePassFilters(RemapModel mapping, ref IEnumerable<TypeDefinition> types)
    {
        if (!FilterTypesByGeneric(mapping, ref types)) return false;
        if (!FilterTypesByMethods(mapping, ref types)) return false;
        if (!FilterTypesByFields(mapping, ref types)) return false;
        if (!FilterTypesByProps(mapping, ref types)) return false;
        if (!FilterTypesByEvents(mapping, ref types)) return false;
        if (!FilterTypesByNested(mapping, ref types)) return false;
        
        return true;
    }
    
	private static bool FilterTypesByGeneric(RemapModel mapping, ref IEnumerable<TypeDefinition> types)
    {
        var parms = mapping.SearchParams;
        
        types = types.Where(t => t.IsPublic == parms.GenericParams.IsPublic);

        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsPublic);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = types.Where(t => t.IsAbstract == parms.GenericParams.IsAbstract);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsAbstract);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = types.Where(t => t.IsSealed == parms.GenericParams.IsSealed);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsSealed);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

        types = types.Where(t => t.IsInterface == parms.GenericParams.IsInterface);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsInterface);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = types.Where(t => t.IsEnum == parms.GenericParams.IsEnum);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsEnum);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = FilterAttributes(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.HasAttribute);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = FilterDerived(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsDerived);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

        types = types.Where(t => t.GenericParameters.Any() == parms.GenericParams.HasGenericParameters);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.HasGenericParameters);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        return true;
    }
    
    private static bool FilterTypesByMethods(RemapModel mapping, ref IEnumerable<TypeDefinition> types)
    {
        types = MethodTypeFilters.FilterByInclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.MethodsInclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = MethodTypeFilters.FilterByExclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.MethodsExclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = MethodTypeFilters.FilterByCount(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.MethodsCount);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

        types = CtorTypeFilters.FilterByParameterCount(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.ConstructorParameterCount);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        return true;
    }

    private static bool FilterTypesByFields(RemapModel mapping, ref IEnumerable<TypeDefinition> types)
    {
        types = FieldTypeFilters.FilterByInclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.FieldsInclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = FieldTypeFilters.FilterByExclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.FieldsExclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = FieldTypeFilters.FilterByCount(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.FieldsCount);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

        return true;
    }

    private static bool FilterTypesByProps(RemapModel mapping, ref IEnumerable<TypeDefinition> types)
    {
        types = PropertyTypeFilters.FilterByInclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.PropertiesInclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = PropertyTypeFilters.FilterByExclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.PropertiesExclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = PropertyTypeFilters.FilterByCount(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.PropertiesCount);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

        return true;
    }

    private static bool FilterTypesByNested(RemapModel mapping, ref IEnumerable<TypeDefinition> types)
    {
        types = NestedTypeFilters.FilterByInclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.NestedTypeInclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = NestedTypeFilters.FilterByExclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.NestedTypeExclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = NestedTypeFilters.FilterByCount(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.NestedTypeCount);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = NestedTypeFilters.FilterByNestedVisibility(types, mapping.SearchParams);

        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.NestedVisibility);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        return true;
    }

    private static bool FilterTypesByEvents(RemapModel mapping, ref IEnumerable<TypeDefinition> types)
    {
        types = EventTypeFilters.FilterByInclude(types, mapping.SearchParams);

        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.EventsInclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

        types = EventTypeFilters.FilterByExclude(types, mapping.SearchParams);

        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.EventsExclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

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
    
    private static void AddNoMatchReason(RemapModel remap, ENoMatchReason noMatchReason)
    {
        remap.NoMatchReasons.Add(noMatchReason);
    }
}