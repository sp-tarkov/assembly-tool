namespace ReCodeItLib.Enums;

public enum ENoMatchReason
{
    AmbiguousWithPreviousMatch,
    AmbiguousNewTypeNames,
    IsPublic,
    IsAbstract,
    IsEnum,
    IsNested,
    IsSealed,
    IsInterface,
    IsDerived,
    HasGenericParameters,
    HasAttribute,
    ConstructorParameterCount,
    MethodsInclude,
    MethodsExclude,
    MethodsCount,
    FieldsInclude,
    FieldsExclude,
    FieldsCount,
    PropertiesInclude,
    PropertiesExclude,
    PropertiesCount,
    NestedTypeInclude,
    NestedTypeExclude,
    NestedTypeCount,
    NestedVisibility,
    EventsInclude,
    EventsExclude
}