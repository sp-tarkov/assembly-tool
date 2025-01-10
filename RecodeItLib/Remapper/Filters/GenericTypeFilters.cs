using dnlib.DotNet;
using ReCodeItLib.Models;
using ReCodeItLib.Utils;

namespace ReCodeItLib.ReMapper.Filters;

internal static class GenericTypeFilters
{
    public static IEnumerable<TypeDef> FilterPublic(IEnumerable<TypeDef> types, SearchParams parms)
    {
        return types.Where(t => t.IsPublic == parms.GenericParams.IsPublic);
    }
    
    public static IEnumerable<TypeDef> FilterAbstract(IEnumerable<TypeDef> types, SearchParams parms)
    {
        // NOTE: Interfaces are abstract
        return types.Where(t => t.IsAbstract == parms.GenericParams.IsAbstract);
    }
    
    public static IEnumerable<TypeDef> FilterSealed(IEnumerable<TypeDef> types, SearchParams parms)
    {
        return types.Where(t => t.IsSealed == parms.GenericParams.IsSealed);
    }
    
    public static IEnumerable<TypeDef> FilterInterface(IEnumerable<TypeDef> types, SearchParams parms)
    {
        // Filter based on interface or not
        if (parms.GenericParams.IsInterface is true)
        {
            types = types.Where(t => t.IsInterface);
        }
        else if (parms.GenericParams.IsInterface is false)
        {
            types = types.Where(t => !t.IsInterface);
        }

        return types;
    }
    
    public static IEnumerable<TypeDef> FilterStruct(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.GenericParams.IsStruct is true)
        {
            types = types.Where(t => t.IsValueType && !t.IsEnum);
        }
        else if (parms.GenericParams.IsStruct is false)
        {
            types = types.Where(t => !t.IsValueType);
        }

        return types;
    }
    
    public static IEnumerable<TypeDef> FilterEnum(IEnumerable<TypeDef> types, SearchParams parms)
    {
        // Filter based on enum or not
        if (parms.GenericParams.IsEnum is true)
        {
            types = types.Where(t => t.IsEnum);
        }
        else if (parms.GenericParams.IsEnum is false)
        {
            types = types.Where(t => !t.IsEnum);
        }

        return types;
    }
    
    public static IEnumerable<TypeDef> FilterAttributes(IEnumerable<TypeDef> types, SearchParams parms)
    {
        // Filter based on HasAttribute or not
        if (parms.GenericParams.HasAttribute is true)
        {
            types = types.Where(t => t.HasCustomAttributes);
        }
        else if (parms.GenericParams.HasAttribute is false)
        {
            types = types.Where(t => !t.HasCustomAttributes);
        }

        return types;
    }
    
    public static IEnumerable<TypeDef> FilterDerived(IEnumerable<TypeDef> types, SearchParams parms)
    {
        // Filter based on IsDerived or not
        if (parms.GenericParams.IsDerived is true)
        {
            types = types.Where(t => t.GetBaseType()?.Name?.String != "Object");

            if (parms.GenericParams.MatchBaseClass is not null and not "")
            {
                types = types.Where(t => t.GetBaseType()?.Name?.String == parms.GenericParams.MatchBaseClass);
            }
        }
        else if (parms.GenericParams.IsDerived is false)
        {
            types = types.Where(t => t.GetBaseType()?.Name?.String is "Object");
        }

        return types;
    }
    
    public static IEnumerable<TypeDef> FilterByGenericParameters(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.GenericParams.HasGenericParameters is null) return types;
        
        types = types.Where(t => t.HasGenericParameters == parms.GenericParams.HasGenericParameters);

        return types;
    }
}