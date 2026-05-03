using System.Reflection;
using AsmResolver.DotNet;

namespace AssemblyLib.Patching.MemberLookup;

public abstract class AbstractMemberLookup
{
    protected abstract ModuleDefinition TargetModule { get; }

    /// <summary>
    /// Looks up a <see cref="TypeDefinition"/> by namespace and type name strings,
    /// bypassing reflection entirely. Use this when typeof(T) would cause a
    /// TypeLoadException due to problematic types in the same assembly.
    /// Supports nested types using '+' as the separator, e.g. "Outer+Inner".
    /// </summary>
    public TypeDefinition? Type(string @namespace, string typeName)
    {
        var parts = typeName.Split('+');
        var outerName = parts[0];

        var current = TargetModule.GetAllTypes().FirstOrDefault(t => t.Namespace == @namespace && t.Name == outerName);

        foreach (var nestedName in parts.Skip(1))
        {
            if (current is null)
            {
                return null;
            }

            current = current.NestedTypes.FirstOrDefault(t => t.Name == nestedName);
        }

        return current;
    }

    /// <summary>
    /// Looks up a <see cref="TypeDefinition"/> by a reflected <see cref="Type"/>.
    /// Falls back to namespace+name search if the token doesn't match the loaded module.
    /// </summary>
    public TypeDefinition? Type(Type type)
    {
        var resolved = LookupByToken<TypeDefinition>(type.MetadataToken);

        if (resolved?.Name == type.Name && resolved.Namespace == type.Namespace)
        {
            return resolved;
        }

        // Nested types: reflection reports the enclosing namespace, but AsmResolver
        // stores them with Namespace == null under the declaring type's NestedTypes.
        if (type.IsNested)
        {
            return ResolveNestedType(type);
        }

        return TargetModule.GetAllTypes().FirstOrDefault(t => t.Namespace == type.Namespace && t.Name == type.Name);
    }

    /// <summary>Looks up a <see cref="TypeDefinition"/> by a generic type parameter.</summary>
    public TypeDefinition? Type<T>() => Type(typeof(T));

    /// <summary>
    /// Looks up a <see cref="FieldDefinition"/> by namespace, type name, and field name,
    /// bypassing reflection entirely.
    /// </summary>
    public FieldDefinition? Field(string @namespace, string typeName, string fieldName) =>
        Type(@namespace, typeName)?.Fields.FirstOrDefault(f => f.Name == fieldName);

    /// <summary>
    /// Looks up a <see cref="FieldDefinition"/> by a reflected <see cref="FieldInfo"/>.
    /// Falls back to name-based search on the declaring type if the token doesn't match.
    /// </summary>
    public FieldDefinition? Field(FieldInfo field)
    {
        var resolved = LookupByToken<FieldDefinition>(field.MetadataToken);

        if (resolved?.Name == field.Name)
        {
            return resolved;
        }

        return Type(field.DeclaringType!)?.Fields.FirstOrDefault(f => f.Name == field.Name);
    }

    /// <summary>
    /// Looks up a <see cref="FieldDefinition"/> by name on the given declaring type.
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

    /// <summary>Looks up a <see cref="FieldDefinition"/> by name on the given type parameter.</summary>
    public FieldDefinition? Field<TDeclaringType>(string fieldName) => Field(typeof(TDeclaringType), fieldName);

    /// <summary>
    /// Looks up a <see cref="MethodDefinition"/> by namespace, type name, and method name,
    /// bypassing reflection entirely.
    /// </summary>
    public MethodDefinition? Method(string @namespace, string typeName, string methodName) =>
        Type(@namespace, typeName)?.Methods.FirstOrDefault(m => m.Name == methodName);

    /// <summary>
    /// Looks up a <see cref="MethodDefinition"/> by namespace, type name, method name,
    /// and parameter count to disambiguate overloads, bypassing reflection entirely.
    /// </summary>
    public MethodDefinition? Method(string @namespace, string typeName, string methodName, int parameterCount) =>
        Type(@namespace, typeName)
            ?.Methods.FirstOrDefault(m => m.Name == methodName && m.Parameters.Count == parameterCount);

    /// <summary>
    /// Looks up a <see cref="MethodDefinition"/> by a reflected <see cref="MethodBase"/>.
    /// Falls back to name-based search on the declaring type if the token doesn't match.
    /// </summary>
    public MethodDefinition? Method(MethodBase method)
    {
        var resolved = LookupByToken<MethodDefinition>(method.MetadataToken);
        if (resolved?.Name == method.Name)
        {
            return resolved;
        }

        var expectedParams = method.GetParameters().Select(p => p.ParameterType.FullName).ToList();

        return Type(method.DeclaringType!)
            ?.Methods.FirstOrDefault(m =>
                m.Name == method.Name
                && m.Parameters.Count == expectedParams.Count
                && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(expectedParams)
            );
    }

    /// <summary>
    /// Looks up a <see cref="MethodDefinition"/> by name on the given declaring type.
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

    /// <summary>Looks up a <see cref="MethodDefinition"/> by name on the given type parameter.</summary>
    public MethodDefinition? Method<TDeclaringType>(string methodName) => Method(typeof(TDeclaringType), methodName);

    /// <summary>
    /// Looks up a <see cref="MethodDefinition"/> by name and explicit parameter types
    /// on the given type parameter.
    /// </summary>
    public MethodDefinition? Method<TDeclaringType>(string methodName, params Type[] parameterTypes) =>
        Method(typeof(TDeclaringType), methodName, parameterTypes);

    /// <summary>
    /// Looks up a <see cref="PropertyDefinition"/> by namespace, type name, and property name,
    /// bypassing reflection entirely.
    /// </summary>
    public PropertyDefinition? Property(string @namespace, string typeName, string propertyName) =>
        Type(@namespace, typeName)?.Properties.FirstOrDefault(p => p.Name == propertyName);

    /// <summary>
    /// Looks up a <see cref="PropertyDefinition"/> by a reflected <see cref="PropertyInfo"/>.
    /// Resolves via the property's getter or setter method token, with name-based fallback.
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

        var viaAccessor = resolvedMethod?.DeclaringType?.Properties.FirstOrDefault(p =>
            p.GetMethod == resolvedMethod || p.SetMethod == resolvedMethod
        );

        if (viaAccessor is not null)
        {
            return viaAccessor;
        }

        // Fallback: find by name on the declaring type
        return Type(property.DeclaringType!)?.Properties.FirstOrDefault(p => p.Name == property.Name);
    }

    /// <summary>
    /// Looks up a <see cref="PropertyDefinition"/> by name on the given declaring type.
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

    /// <summary>Looks up a <see cref="PropertyDefinition"/> by name on the given type parameter.</summary>
    public PropertyDefinition? Property<TDeclaringType>(string propertyName) =>
        Property(typeof(TDeclaringType), propertyName);

    /// <summary>
    /// Looks up an <see cref="EventDefinition"/> by a reflected <see cref="EventInfo"/>.
    /// Resolves via the event's add/remove accessor method token, with name-based fallback.
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

        var viaAccessor = resolvedMethod?.DeclaringType?.Events.FirstOrDefault(e =>
            e.AddMethod == resolvedMethod || e.RemoveMethod == resolvedMethod
        );

        if (viaAccessor is not null)
        {
            return viaAccessor;
        }

        // Fallback: find by name on the declaring type
        return Type(evt.DeclaringType!)?.Events.FirstOrDefault(e => e.Name == evt.Name);
    }

    /// <summary>
    /// Looks up an <see cref="EventDefinition"/> by name on the given declaring type.
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

    /// <summary>Looks up an <see cref="EventDefinition"/> by name on the given type parameter.</summary>
    public EventDefinition? Event<TDeclaringType>(string eventName) => Event(typeof(TDeclaringType), eventName);

    /// <summary>
    /// Directly looks up any <see cref="IMemberDefinition"/> by a raw metadata token value.
    /// No fallback is applied — the token is used as-is.
    /// </summary>
    public TMember? Lookup<TMember>(int metadataToken)
        where TMember : class, IMemberDefinition => LookupByToken<TMember>((uint)metadataToken);

    /// <summary>
    /// Directly looks up any <see cref="IMemberDefinition"/> by a raw metadata token value.
    /// No fallback is applied — the token is used as-is.
    /// </summary>
    public TMember? Lookup<TMember>(uint metadataToken)
        where TMember : class, IMemberDefinition => LookupByToken<TMember>(metadataToken);

    private TMember? LookupByToken<TMember>(int metadataToken)
        where TMember : class, IMemberDefinition => LookupByToken<TMember>((uint)metadataToken);

    private TMember? LookupByToken<TMember>(uint metadataToken)
        where TMember : class, IMemberDefinition
    {
        try
        {
            return TargetModule.LookupMember<TMember>(metadataToken);
        }
        catch
        {
            // Token is out of range or wrong table for this module — fallback will handle it
            return null;
        }
    }

    /// <summary>
    /// Walks the full declaring-type chain from outermost to innermost,
    /// following <see cref="TypeDefinition.NestedTypes"/> at each level.
    /// </summary>
    private TypeDefinition? ResolveNestedType(Type type)
    {
        // Build the chain of declaring types from outermost inward, e.g.:
        //   TarkovApplication -> CG_Struct35
        var chain = new Stack<Type>();
        var cursor = type;
        while (cursor is not null)
        {
            chain.Push(cursor);
            cursor = cursor.DeclaringType;
        }

        // Resolve the outermost (non-nested) type first
        var outermost = chain.Pop();
        var current = TargetModule
            .GetAllTypes()
            .FirstOrDefault(t => t.Namespace == outermost.Namespace && t.Name == outermost.Name);

        // Walk inward through each nested level
        while (current is not null && chain.Count > 0)
        {
            var nested = chain.Pop();
            current = current.NestedTypes.FirstOrDefault(t => t.Name == nested.Name);
        }

        return current;
    }
}
