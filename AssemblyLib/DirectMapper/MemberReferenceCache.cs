using System.Collections.Concurrent;
using AsmResolver.DotNet;
using AssemblyLib.Shared;
using Serilog;
using Spectre.Console;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable(InjectionType.Singleton)]
public class MemberReferenceCache(DataProvider dataProvider)
{
    private readonly ConcurrentDictionary<FieldDefinition, List<MemberReference>> _fieldReferences = [];
    private readonly ConcurrentDictionary<MethodDefinition, List<MemberReference>> _methodReferences = [];
    private readonly ConcurrentDictionary<PropertyDefinition, List<MemberReference>> _propertyReferences = [];
    private readonly ConcurrentDictionary<MethodDefinition, List<MethodDefinition>> _methodOverrides = [];

    private bool _hydrated;

    public async Task Hydrate()
    {
        if (_hydrated)
        {
            return;
        }

        Log.Information("Hydrating MemberReferenceCache");

        CacheMethodReferences();
        //await CacheMethodOverrides();

        Log.Information("Field definition cache hydrated with {count} field definitions", _fieldReferences.Count);
        Log.Information("Method definition cache hydrated with {count} method definitions", _methodReferences.Count);
        Log.Information(
            "Property definition cache hydrated with {count} property definitions",
            _propertyReferences.Count
        );
        Log.Information(
            "Method override cache hydrated with {count} methods that are overriden",
            _methodOverrides.Count
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

    public List<MethodDefinition> GetMethodOverrides(MethodDefinition method)
    {
        if (!_hydrated)
        {
            throw new InvalidOperationException("MemberReferenceCache has not been hydrated");
        }

        return _methodOverrides.TryGetValue(method, out var value) ? value : [];
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
            var canResolve = reference.TryResolve(dataProvider.Context, out var resolved);

            if (canResolve)
            {
                AddMetadataMemberToCache(resolved, reference);
            }
            else
            {
                Log.Warning("Could not resolve {ReferenceFullName}", reference.FullName);
            }
        }
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

    private async Task CacheMethodOverrides()
    {
        var allMethods = dataProvider.LoadedModule!.GetAllTypes().SelectMany(t => t.Methods.Where(m => m.IsNewSlot));

        await AnsiConsole
            .Progress()
            .AutoClear(true)
            .StartAsync(ctx =>
            {
                var ctxTask = ctx.AddTask(
                    "[green]Finding method overrides[/]".PadLeft(25),
                    maxValue: allMethods.Count()
                );
                var tasks = new List<Task>(allMethods.Count());

                foreach (var method in allMethods)
                {
                    tasks.Add(
                        Task.Factory.StartNew(async () =>
                        {
                            await FindAllMethodOverrides(method);
                            ctxTask.Increment(1.0);
                        })
                    );
                }

                return Task.WhenAll(tasks);
            });
    }

    private Task FindAllMethodOverrides(MethodDefinition method)
    {
        var baseType = method.DeclaringType;
        var overrides = dataProvider
            .LoadedModule!.GetAllTypes()
            // Only look at types that inherit from this methods base type
            .Where(t => t.InheritsFrom(baseType!.FullName))
            .SelectMany(t => t.Methods)
            // New slot indicates that a method is inserted into the VTable, therefore not an override
            // Overrides are also virtual, so look for those
            .Where(m => !m.IsNewSlot && m.IsVirtual && m.Name == method.Name)
            .ToList();

        if (overrides.Count == 0)
        {
            return Task.CompletedTask;
        }

        _methodOverrides.TryAdd(method, overrides);

        return Task.CompletedTask;
    }
}
