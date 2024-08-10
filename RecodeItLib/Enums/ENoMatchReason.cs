namespace ReCodeIt.Enums;

public enum ENoMatchReason
{
    AmbiguousWithPreviousMatch,
    AmbiguousNewTypeNames,
    IsPublic,
    IsEnum,
    IsNested,
    IsSealed,
    IsInterface,
    IsStruct,
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
}