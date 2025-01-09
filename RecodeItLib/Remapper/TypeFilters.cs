using dnlib.DotNet;
using ReCodeItLib.Enums;
using ReCodeItLib.Models;
using ReCodeItLib.ReMapper.Filters;
using ReCodeItLib.Utils;

namespace ReCodeItLib.ReMapper;

public class TypeFilters
{
    public bool DoesTypePassFilters(RemapModel mapping, ref IEnumerable<TypeDef> types)
    {
        if (!FilterTypesByGeneric(mapping, ref types)) return false;
        if (!FilterTypesByMethods(mapping, ref types)) return false;
        if (!FilterTypesByFields(mapping, ref types)) return false;
        if (!FilterTypesByProps(mapping, ref types)) return false;
        if (!FilterTypesByEvents(mapping, ref types)) return false;
        if (!FilterTypesByNested(mapping, ref types)) return false;
        
        return true;
    }
    
	private static bool FilterTypesByGeneric(RemapModel mapping, ref IEnumerable<TypeDef> types)
    {
        types = GenericTypeFilters.FilterPublic(types, mapping.SearchParams);

        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsPublic);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterAbstract(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsAbstract);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterSealed(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsSealed);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterInterface(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsInterface);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterStruct(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsStruct);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterEnum(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsEnum);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterAttributes(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.HasAttribute);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterDerived(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.IsDerived);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterByGenericParameters(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AddNoMatchReason(mapping, ENoMatchReason.HasGenericParameters);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        return true;
    }
    
    private static bool FilterTypesByMethods(RemapModel mapping, ref IEnumerable<TypeDef> types)
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

    private static bool FilterTypesByFields(RemapModel mapping, ref IEnumerable<TypeDef> types)
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

    private static bool FilterTypesByProps(RemapModel mapping, ref IEnumerable<TypeDef> types)
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

    private static bool FilterTypesByNested(RemapModel mapping, ref IEnumerable<TypeDef> types)
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

    private static bool FilterTypesByEvents(RemapModel mapping, ref IEnumerable<TypeDef> types)
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
    
    private static void AddNoMatchReason(RemapModel remap, ENoMatchReason noMatchReason)
    {
        remap.NoMatchReasons.Add(noMatchReason);
    }
}