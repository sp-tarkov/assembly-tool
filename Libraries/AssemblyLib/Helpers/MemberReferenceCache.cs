using System.Collections.Concurrent;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Helpers;

[Injectable(InjectionType.Singleton)]
public class MemberReferenceCache(ILogger<MemberReferenceCache> logger, DataProvider dataProvider)
{
    private readonly ConcurrentDictionary<FieldDefinition, List<MemberReference>> _fieldReferences = [];
    private readonly ConcurrentDictionary<MethodDefinition, List<MemberReference>> _methodReferences = [];
    private readonly ConcurrentDictionary<PropertyDefinition, List<MemberReference>> _propertyReferences = [];

    private bool _hydrated;

    public void Hydrate()
    {
        if (_hydrated)
        {
            return;
        }

        logger.LogInformation("Hydrating MemberReferenceCache");

        CacheMethodReferences();

        logger.LogInformation("Field definition cache hydrated with {count} field definitions", _fieldReferences.Count);
        logger.LogInformation(
            "Method definition cache hydrated with {count} method definitions",
            _methodReferences.Count
        );
        logger.LogInformation(
            "Property definition cache hydrated with {count} property definitions",
            _propertyReferences.Count
        );

        _hydrated = true;
    }

    public List<MemberReference> GetFieldReferences(FieldDefinition field)
    {
        if (!_hydrated)
        {
            throw new InvalidOperationException("MemberReferenceCache has not been hydrated");
        }

        return _fieldReferences.TryGetValue(field, out var value)
            ? value
            : throw new KeyNotFoundException($"Field {field.FullName} does not exist in cache");
    }

    public List<MemberReference> GetMethodReferences(MethodDefinition method)
    {
        if (!_hydrated)
        {
            throw new InvalidOperationException("MemberReferenceCache has not been hydrated");
        }

        return _methodReferences.TryGetValue(method, out var value)
            ? value
            : throw new KeyNotFoundException($"Method {method.FullName} does not exist in cache");
    }

    public List<MemberReference> GetPropertyReferences(PropertyDefinition property)
    {
        if (!_hydrated)
        {
            throw new InvalidOperationException("MemberReferenceCache has not been hydrated");
        }

        return _propertyReferences.TryGetValue(property, out var value)
            ? value
            : throw new KeyNotFoundException($"Property {property.FullName} does not exist in cache");
    }

    private void CacheMethodReferences()
    {
        foreach (var type in dataProvider.LoadedModule!.GetAllTypes())
        {
            foreach (var field in type.Fields)
            {
                _fieldReferences.TryAdd(field, []);
            }

            foreach (var method in type.Methods)
            {
                _methodReferences.TryAdd(method, []);
            }

            foreach (var property in type.Properties)
            {
                _propertyReferences.TryAdd(property, []);
            }
        }

        foreach (var reference in dataProvider.LoadedModule!.GetImportedMemberReferences())
        {
            if (reference.TryResolve(dataProvider.Context, out var resolved))
            {
                AddMetadataMemberToCache(resolved, reference);
            }
            else
            {
                // TryResolve fails for MemberReferences whose declaring type is a generic
                // instantiation (TypeSpecification). Unwrap the open generic and match by name.
                var genericResolved = TryResolveViaGenericType(reference);
                if (genericResolved is not null)
                {
                    AddMetadataMemberToCache(genericResolved, reference);
                }
            }
        }
    }

    private IMemberDefinition? TryResolveViaGenericType(MemberReference reference)
    {
        if (reference.DeclaringType is not TypeSpecification { Signature: GenericInstanceTypeSignature genericSig })
        {
            return null;
        }

        var typeDef = genericSig.GenericType.Resolve(dataProvider.Context);
        if (typeDef is null)
        {
            return null;
        }

        var name = reference.Name;
        var paramCount = (reference.Signature as MethodSignature)?.ParameterTypes.Count;

        if (paramCount is not null)
        {
            return typeDef.Methods.FirstOrDefault(m => m.Name == name && m.Parameters.Count == paramCount.Value);
        }

        return typeDef.Fields.FirstOrDefault(f => f.Name == name);
    }

    private void AddMetadataMemberToCache(IMemberDefinition? definition, MemberReference reference)
    {
        switch (definition)
        {
            case FieldDefinition field:
            {
                if (_fieldReferences.TryGetValue(field, out var list))
                {
                    list.Add(reference);
                }

                break;
            }
            case MethodDefinition method:
            {
                if (_methodReferences.TryGetValue(method, out var list))
                {
                    list.Add(reference);
                }

                break;
            }
            case PropertyDefinition property:
            {
                if (_propertyReferences.TryGetValue(property, out var list))
                {
                    list.Add(reference);
                }

                break;
            }
        }
    }
}
