using AsmResolver.DotNet;
using AssemblyLib.Exceptions;
using AssemblyLib.Extensions;
using AssemblyLib.Models;
using Serilog;
using Serilog.Events;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Shared;

[Injectable(InjectionType.Singleton)]
public sealed class TypeCache(DataProvider dataProvider)
{
    private bool _isHydrated;

    /// <summary>
    ///     Contains all classes that are NOT nested, sealed, or abstract
    /// </summary>
    private List<TypeDefinition>? _classes;

    /// <summary>
    ///     Contains all abstract classes
    /// </summary>
    private List<TypeDefinition>? _abstractClasses;

    /// <summary>
    ///     Contains all sealed classes
    /// </summary>
    private List<TypeDefinition>? _sealedClasses;

    /// <summary>
    ///     Contains all static classes (abstract and sealed)
    /// </summary>
    private List<TypeDefinition>? _staticClasses;

    /// <summary>
    ///     Contains all nested classes, can be sealed or abstract
    /// </summary>
    private List<TypeDefinition>? _nestedClasses;

    /// <summary>
    ///     Contains all non-nested, non-readonly structs
    /// </summary>
    private List<TypeDefinition>? _structs;

    /// <summary>
    ///     Contains all nested Structs
    /// </summary>
    private List<TypeDefinition>? _nestedStructs;

    /// <summary>
    ///     Contains all interfaces
    /// </summary>
    private List<TypeDefinition>? _interfaces;

    /// <summary>
    ///     Contains all enums
    /// </summary>
    private List<TypeDefinition>? _enums;

    public void HydrateCache()
    {
        if (_isHydrated)
        {
            Log.Warning("Trying to hydrate an already hydrated type cache");
            return;
        }

        if (!dataProvider.IsModuleLoaded)
        {
            throw new InvalidOperationException("Module is not loaded when trying to hydrate type cache");
        }

        // Load all types except the ones that are compiler generated, they should NEVER be a target of a remap.
        // Also filter out named classes, we NEVER want to rename an already named class, so don't put them up for consideration
        var allTypes = dataProvider
            .LoadedModule?.GetAllTypes()
            .Where(t => !t.IsCompilerGenerated() && (t.Name?.IsObfuscatedName() ?? false));

        if (allTypes is null || !allTypes.Any())
        {
            throw new NullReferenceException("Could not get types from module, something has gone horrible wrong.");
        }

        var classes = allTypes.Where(t => t.IsClass);

        _classes = classes.Where(t => !t.IsAbstract && !t.IsSealed && !t.IsNested).ToList();
        _abstractClasses = classes.Where(t => t.IsAbstract).ToList();
        _sealedClasses = classes.Where(t => t.IsSealed).ToList();
        _staticClasses = classes.Where(t => t.IsAbstract && t.IsSealed).ToList();

        _nestedClasses = classes.Where(t => t.IsNested).ToList();

        _structs = allTypes.Where(t => t.IsValueType && !t.IsNested && !t.IsEnum).ToList();
        _nestedStructs = allTypes.Where(t => t.IsValueType && t.IsNested && !t.IsEnum).ToList();

        _interfaces = allTypes.Where(t => t.IsInterface).ToList();
        _enums = allTypes.Where(t => t.IsEnum).ToList();

        Log.Information("-------------------------------- Cache Hydrated --------------------------------");
        Log.Information("Loaded: {num} Total obfuscated types", allTypes.Count());
        Log.Information("Loaded: {num} Non-nested, sealed, or abstract classes", _classes.Count);
        Log.Information("Loaded: {num} Abstract classes", _abstractClasses.Count);
        Log.Information("Loaded: {num} Sealed classes", _sealedClasses.Count);
        Log.Information("Loaded: {num} Static classes", _staticClasses.Count);
        Log.Information("Loaded: {num} Nested classes", _nestedClasses.Count);

        Log.Information("Loaded: {num} Non-nested structs", _structs.Count);
        Log.Information("Loaded: {num} Nested structs", _nestedStructs.Count);

        Log.Information("Loaded: {num} Interfaces", _interfaces.Count);
        Log.Information("Loaded: {num} Enums", _enums.Count);
        Log.Information("--------------------------------------------------------------------------------");

        _isHydrated = true;
    }

    /// <summary>
    ///     Select the correct cache for the provided remap
    /// </summary>
    /// <param name="remapModel">Remap to select the cache for</param>
    /// <returns>Selected cache</returns>
    public List<TypeDefinition> SelectCache(RemapModel remapModel)
    {
        var genericParams = remapModel.SearchParams.GenericParams;
        var nestedParams = remapModel.SearchParams.NestedTypes;

        // Order here is very important do NOT change it, Otherwise cases such as static can be missed

        /* Type             IL designation
         * Interface:       .class interface public auto ansi abstract beforefieldinit
         * Class:           .class public auto ansi beforefieldinit
         * Abstract Class:  .class public auto ansi abstract beforefieldinit
         * Sealed Class:    .class public auto ansi sealed beforefieldinit
         * Static Class:    .class public auto ansi abstract sealed beforefieldinit
         * Structs:         .class public sequential ansi sealed beforefieldinit [Name] extends [System.Runtime]System.ValueType
         */

        // Abstract and sealed = static
        if (genericParams.IsStatic)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Static class cache chosen for remap: {newTypeName}", remapModel.NewTypeName);
            }

            return _staticClasses ?? throw new TypeCacheException("Static class cache is null");
        }

        if (genericParams.IsStruct ?? false)
        {
            if (nestedParams.IsNested)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Nested struct cache chosen for remap: {newTypeName}", remapModel.NewTypeName);
                }

                return _nestedStructs ?? throw new TypeCacheException("NestedStructs cache is null");
            }

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Struct cache chosen for remap: {newTypeName}", remapModel.NewTypeName);
            }

            return _structs ?? throw new TypeCacheException("Structs cache is null");
        }

        // Interface - considered abstract so check it before abstract
        if (genericParams.IsInterface)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Interface cache chosen for remap: {newTypeName}", remapModel.NewTypeName);
            }

            return _interfaces ?? throw new TypeCacheException("Interfaces cache is null");
        }

        if (genericParams.IsAbstract)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Abstract class cache chosen for remap: {newTypeName}", remapModel.NewTypeName);
            }

            return _abstractClasses ?? throw new TypeCacheException("AbstractClasses cache is null");
        }

        if (genericParams.IsSealed)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Sealed class cache chosen for remap: {newTypeName}", remapModel.NewTypeName);
            }

            return _sealedClasses ?? throw new TypeCacheException("SealedClasses cache is null");
        }

        // Enums are never obfuscated but im putting this here anyway just in-case
        if (genericParams.IsEnum)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Enum cache chosen for remap: {newTypeName}", remapModel.NewTypeName);
            }

            return _enums ?? throw new TypeCacheException("Enum cache is null");
        }

        // Last thing to consider is if its nested or not
        switch (nestedParams.IsNested)
        {
            case true:
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Nested class cache chosen for remap: {newTypeName}", remapModel.NewTypeName);
                }

                return _nestedClasses ?? throw new TypeCacheException("NestedClasses cache is null");
            case false:
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Class cache chosen for remap: {newTypeName}", remapModel.NewTypeName);
                }

                return _classes ?? throw new TypeCacheException("Classes cache is null");
        }
    }

    public List<TypeDefinition> SelectCache(TypeDefinition typeDef)
    {
        var strName = typeDef.Name?.ToString() ?? "Type Name is null";

        // Abstract and sealed = static
        if (typeDef.IsStatic())
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Static class cache chosen for type: {name}", strName);
            }

            return _staticClasses ?? throw new TypeCacheException("Static class cache is null");
        }

        if (typeDef.IsStruct())
        {
            if (typeDef.IsNested)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Nested struct cache chosen for type: {name}", strName);
                }

                return _nestedStructs ?? throw new TypeCacheException("NestedStructs cache is null");
            }

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Struct cache chosen for type: {name}", strName);
            }

            return _structs ?? throw new TypeCacheException("Structs cache is null");
        }

        if (typeDef.IsInterface)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Interface cache chosen for type: {name}", strName);
            }

            return _interfaces ?? throw new TypeCacheException("Interfaces cache is null");
        }

        if (typeDef.IsAbstract)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Abstract class cache chosen for type: {name}", strName);
            }

            return _abstractClasses ?? throw new TypeCacheException("AbstractClasses cache is null");
        }

        if (typeDef.IsSealed)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Sealed class cache chosen for type: {name}", strName);
            }

            return _sealedClasses ?? throw new TypeCacheException("SealedClasses cache is null");
        }

        if (typeDef.IsEnum)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Enum cache chosen for type: {name}", strName);
            }

            return _enums ?? throw new TypeCacheException("Enum cache is null");
        }

        switch (typeDef.IsNested)
        {
            case true:
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Nested class cache chosen for remap: {name}", strName);
                }

                return _nestedClasses ?? throw new TypeCacheException("NestedClasses cache is null");
            case false:
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Class cache chosen for remap: {name}", strName);
                }

                return _classes ?? throw new TypeCacheException("Classes cache is null");
        }
    }
}
