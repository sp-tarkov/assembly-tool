using dnlib.DotNet;
using ReCodeIt.Models;
using ReCodeIt.Utils;

namespace ReCodeItLib.Remapper.Search;

internal static class GenericTypeFilters
{
    /// <summary>
    /// Filters based on public, or nested public or private if the nested flag is set. This is a
    /// required property
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterPublic(IEnumerable<TypeDef> types, SearchParams parms)
    {
        // REQUIRED PROPERTY
        if (parms.IsPublic)
        {
            if (parms.IsNested is true)
            {
                types = types.Where(t => t.IsNestedPublic);

                types = FilterNestedByName(types, parms);
            }
            else
            {
                types = types.Where(t => t.IsPublic);
            }
        }
        else
        {
            if (parms.IsNested is true)
            {
                types = types.Where(t => t.IsNestedPrivate
                                         || t.IsNestedFamily
                                         || t.IsNestedFamilyAndAssembly
                                         || t.IsNestedAssembly);

                types = FilterNestedByName(types, parms);
            }
            else
            {
                types = types.Where(t => t.IsNotPublic);
            }
        }

        return types;
    }

    private static IEnumerable<TypeDef> FilterNestedByName(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.NTParentName is not null)
        {
            types = types.Where(t => t.DeclaringType.Name.String == parms.NTParentName);
        }

        return types;
    }

    /// <summary>
    /// Filters based on IsAbstract
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterAbstract(IEnumerable<TypeDef> types, SearchParams parms)
    {
        // Filter based on abstract or not
        if (parms.IsAbstract is true)
        {
            types = types.Where(t => t.IsAbstract && !t.IsInterface);
        }
        else if (parms.IsAbstract is false)
        {
            types = types.Where(t => !t.IsAbstract);
        }

        return types;
    }

    /// <summary>
    /// Filters based on IsAbstract
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterSealed(IEnumerable<TypeDef> types, SearchParams parms)
    {
        // Filter based on abstract or not
        if (parms.IsSealed is true)
        {
            types = types.Where(t => t.IsSealed);
        }
        else if (parms.IsSealed is false)
        {
            types = types.Where(t => !t.IsSealed);
        }

        return types;
    }

    /// <summary>
    /// Filters based on IsInterface
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterInterface(IEnumerable<TypeDef> types, SearchParams parms)
    {
        // Filter based on interface or not
        if (parms.IsInterface is true)
        {
            types = types.Where(t => t.IsInterface);
        }
        else if (parms.IsInterface is false)
        {
            types = types.Where(t => !t.IsInterface);
        }

        return types;
    }

    /// <summary>
    /// Filters based on IsStruct
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterStruct(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.IsStruct is true)
        {
            types = types.Where(t => t.IsValueType && !t.IsEnum);
        }
        else if (parms.IsStruct is false)
        {
            types = types.Where(t => !t.IsValueType);
        }

        return types;
    }

    /// <summary>
    /// Filters based on IsEnum
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterEnum(IEnumerable<TypeDef> types, SearchParams parms)
    {
        // Filter based on enum or not
        if (parms.IsEnum is true)
        {
            types = types.Where(t => t.IsEnum);
        }
        else if (parms.IsEnum is false)
        {
            types = types.Where(t => !t.IsEnum);
        }

        return types;
    }

    /// <summary>
    /// Filters based on HasAttribute
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterAttributes(IEnumerable<TypeDef> types, SearchParams parms)
    {
        // Filter based on HasAttribute or not
        if (parms.HasAttribute is true)
        {
            types = types.Where(t => t.HasCustomAttributes);
        }
        else if (parms.HasAttribute is false)
        {
            types = types.Where(t => !t.HasCustomAttributes);
        }

        return types;
    }

    /// <summary>
    /// Filters based on HasAttribute
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterDerived(IEnumerable<TypeDef> types, SearchParams parms)
    {
        // Filter based on IsDerived or not
        if (parms.IsDerived is true)
        {
            types = types.Where(t => t.GetBaseType()?.Name?.String != "Object");

            if (parms.MatchBaseClass is not null and not "")
            {
                types = types.Where(t => t.GetBaseType()?.Name?.String == parms.MatchBaseClass);
            }

            if (parms.IgnoreBaseClass is not null and not "")
            {
                types = types.Where(t => t.GetBaseType()?.Name?.String != parms.IgnoreBaseClass);
            }
        }
        else if (parms.IsDerived is false)
        {
            types = types.Where(t => t.GetBaseType()?.Name?.String is "Object");
        }

        return types;
    }

    /// <summary>
    /// Filters based on method count
    /// </summary>
    /// <param name="types"></param>
    /// <param name="parms"></param>
    /// <returns>Filtered list</returns>
    public static IEnumerable<TypeDef> FilterByGenericParameters(IEnumerable<TypeDef> types, SearchParams parms)
    {
        if (parms.HasGenericParameters is null) return types;
        
        types = types.Where(t => t.HasGenericParameters == parms.HasGenericParameters);

        return types;
    }
}