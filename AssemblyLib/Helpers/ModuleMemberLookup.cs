using System.Reflection;
using AsmResolver.DotNet;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Helpers;

[Injectable]
public class ModuleMemberLookup(DataProvider dataProvider)
{
    /// <summary>Looks up a <see cref="TypeDefinition"/> by a reflected <see cref="Type"/>.</summary>
    public TypeDefinition? Type(Type type) => Lookup<TypeDefinition>(type.MetadataToken);

    /// <summary>Looks up a <see cref="TypeDefinition"/> by a generic type parameter.</summary>
    public TypeDefinition? Type<T>() => Type(typeof(T));

    /// <summary>Looks up a <see cref="FieldDefinition"/> by a reflected <see cref="FieldInfo"/>.</summary>
    public FieldDefinition? Field(FieldInfo field) => Lookup<FieldDefinition>(field.MetadataToken);

    /// <summary>
    /// Looks up a <see cref="FieldDefinition"/> by name on a given type.
    /// Searches public and non-public instance and static fields.
    /// </summary>
    public FieldDefinition? Field(Type declaringType, string fieldName)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        var fieldInfo =
            declaringType.GetField(fieldName, flags)
            ?? throw new MissingFieldException(declaringType.FullName, fieldName);

        return Field(fieldInfo);
    }

    /// <summary>Looks up a <see cref="FieldDefinition"/> by name on a given type parameter.</summary>
    public FieldDefinition? Field<TDeclaringType>(string fieldName) => Field(typeof(TDeclaringType), fieldName);

    /// <summary>Looks up a <see cref="MethodDefinition"/> by a reflected <see cref="MethodBase"/>.</summary>
    public MethodDefinition? Method(MethodBase method) => Lookup<MethodDefinition>(method.MetadataToken);

    /// <summary>
    /// Looks up a <see cref="MethodDefinition"/> by name on a given type.
    /// Throws if the name is ambiguous — use the overload with parameter types to disambiguate.
    /// </summary>
    public MethodDefinition? Method(Type declaringType, string methodName)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        var methodInfo =
            declaringType.GetMethod(methodName, flags)
            ?? throw new MissingMethodException(declaringType.FullName, methodName);

        return Method(methodInfo);
    }

    /// <summary>
    /// Looks up a <see cref="MethodDefinition"/> by name and explicit parameter types,
    /// allowing disambiguation of overloaded methods.
    /// </summary>
    public MethodDefinition? Method(Type declaringType, string methodName, params Type[] parameterTypes)
    {
        var methodInfo =
            declaringType.GetMethod(methodName, parameterTypes)
            ?? throw new MissingMethodException(declaringType.FullName, methodName);

        return Method(methodInfo);
    }

    /// <summary>Looks up a <see cref="MethodDefinition"/> by name on a given type parameter.</summary>
    public MethodDefinition? Method<TDeclaringType>(string methodName) => Method(typeof(TDeclaringType), methodName);

    /// <summary>
    /// Looks up a <see cref="MethodDefinition"/> by name and explicit parameter types
    /// on a given type parameter.
    /// </summary>
    public MethodDefinition? Method<TDeclaringType>(string methodName, params Type[] parameterTypes) =>
        Method(typeof(TDeclaringType), methodName, parameterTypes);

    /// <summary>
    /// Looks up a <see cref="PropertyDefinition"/> by a reflected <see cref="PropertyInfo"/>.
    /// Resolves via the property's getter or setter method token.
    /// </summary>
    public PropertyDefinition? Property(PropertyInfo property)
    {
        var accessor =
            property.GetGetMethod(nonPublic: true)
            ?? property.GetSetMethod(nonPublic: true)
            ?? throw new ArgumentException(
                $"Property '{property.Name}' on '{property.DeclaringType?.FullName}' has no accessible accessor.",
                nameof(property)
            );

        var resolvedMethod = Method(accessor);

        return resolvedMethod?.DeclaringType?.Properties.FirstOrDefault(p =>
            p.GetMethod == resolvedMethod || p.SetMethod == resolvedMethod
        );
    }

    /// <summary>
    /// Looks up a <see cref="PropertyDefinition"/> by name on a given type.
    /// Searches public and non-public instance and static properties.
    /// </summary>
    public PropertyDefinition? Property(Type declaringType, string propertyName)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        var propertyInfo =
            declaringType.GetProperty(propertyName, flags)
            ?? throw new MissingMemberException(declaringType.FullName, propertyName);

        return Property(propertyInfo);
    }

    /// <summary>Looks up a <see cref="PropertyDefinition"/> by name on a given type parameter.</summary>
    public PropertyDefinition? Property<TDeclaringType>(string propertyName) =>
        Property(typeof(TDeclaringType), propertyName);

    /// <summary>
    /// Looks up an <see cref="EventDefinition"/> by a reflected <see cref="EventInfo"/>.
    /// Resolves via the event's add or remove accessor method token.
    /// </summary>
    public EventDefinition? Event(EventInfo evt)
    {
        var accessor =
            evt.GetAddMethod(nonPublic: true)
            ?? evt.GetRemoveMethod(nonPublic: true)
            ?? throw new ArgumentException(
                $"Event '{evt.Name}' on '{evt.DeclaringType?.FullName}' has no accessible accessor.",
                nameof(evt)
            );

        var resolvedMethod = Method(accessor);

        return resolvedMethod?.DeclaringType?.Events.FirstOrDefault(e =>
            e.AddMethod == resolvedMethod || e.RemoveMethod == resolvedMethod
        );
    }

    /// <summary>
    /// Looks up an <see cref="EventDefinition"/> by name on a given type.
    /// Searches public and non-public instance and static events.
    /// </summary>
    public EventDefinition? Event(Type declaringType, string eventName)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        var eventInfo =
            declaringType.GetEvent(eventName, flags)
            ?? throw new MissingMemberException(declaringType.FullName, eventName);

        return Event(eventInfo);
    }

    /// <summary>Looks up an <see cref="EventDefinition"/> by name on a given type parameter.</summary>
    public EventDefinition? Event<TDeclaringType>(string eventName) => Event(typeof(TDeclaringType), eventName);

    /// <summary>
    /// Directly looks up any <see cref="IMemberDefinition"/> by a raw metadata token value.
    /// Useful when you already have a token from another source.
    /// </summary>
    public TMember? Lookup<TMember>(int metadataToken)
        where TMember : class, IMemberDefinition =>
        dataProvider.LoadedModule?.LookupMember<TMember>((uint)metadataToken);

    /// <summary>
    /// Directly looks up any <see cref="IMemberDefinition"/> by a raw metadata token value.
    /// </summary>
    public TMember? Lookup<TMember>(uint metadataToken)
        where TMember : class, IMemberDefinition => dataProvider.LoadedModule?.LookupMember<TMember>(metadataToken);
}
