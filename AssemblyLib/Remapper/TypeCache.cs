using AsmResolver.DotNet;
using AssemblyLib.Utils;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Remapper;

[Injectable(InjectionType.Singleton)]
public class TypeCache(DataProvider dataProvider)
{
    /// <summary>
    ///     Is the cache hydrated?
    /// </summary>
    public bool IsHydrated { get; private set; }

    /// <summary>
    ///     Contains all classes that are NOT nested, sealed, or abstract
    /// </summary>
    public List<TypeDefinition>? Classes { get; private set; }

    /// <summary>
    ///     Contains all abstract classes
    /// </summary>
    public List<TypeDefinition>? AbstractClasses { get; private set; }

    /// <summary>
    ///     Contains all nested classes
    /// </summary>
    public List<TypeDefinition>? NestedClasses { get; private set; }

    /// <summary>
    ///     Contains all sealed classes
    /// </summary>
    public List<TypeDefinition>? SealedClasses { get; private set; }

    /// <summary>
    ///     Contains all non-nested structs
    /// </summary>
    public List<TypeDefinition>? Structs { get; private set; }

    /// <summary>
    ///     Contains all nested Structs
    /// </summary>
    public List<TypeDefinition>? NestedStructs { get; private set; }

    /// <summary>
    ///     Contains all interfaces
    /// </summary>
    public List<TypeDefinition>? Interfaces { get; private set; }

    /// <summary>
    ///     Contains all enums
    /// </summary>
    public List<TypeDefinition>? Enums { get; private set; }

    public void HydrateCache()
    {
        if (IsHydrated)
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

        Log.Information("Hydrating TypeCache");

        var classes = allTypes.Where(t => t.IsClass);
        Classes = classes.Where(t => !t.IsAbstract && !t.IsSealed && !t.IsNested).ToList();
        AbstractClasses = classes.Where(t => t.IsAbstract).ToList();
        NestedClasses = classes.Where(t => t.IsNested).ToList();
        SealedClasses = classes.Where(t => t.IsSealed).ToList();

        Structs = allTypes.Where(t => t.IsValueType && !t.IsNested && !t.IsEnum).ToList();
        NestedStructs = allTypes.Where(t => t.IsValueType && t.IsNested && !t.IsEnum).ToList();

        Interfaces = allTypes.Where(t => t.IsInterface).ToList();
        Enums = allTypes.Where(t => t.IsEnum).ToList();

        Log.Information("-------------------------------- Cache Hydrated --------------------------------");
        Log.Information("Loaded: {num} Total obfuscated types", allTypes.Count());
        Log.Information("Loaded: {num} Non-nested, sealed, or abstract classes", Classes.Count);
        Log.Information("Loaded: {num} Abstract classes", AbstractClasses.Count);
        Log.Information("Loaded: {num} Nested classes", NestedClasses.Count);
        Log.Information("Loaded: {num} Sealed classes", SealedClasses.Count);

        Log.Information("Loaded: {num} Non-nested structs", Structs.Count);
        Log.Information("Loaded: {num} Nested structs", NestedStructs.Count);

        Log.Information("Loaded: {num} Interfaces", Interfaces.Count);
        Log.Information("Loaded: {num} Enums", Interfaces.Count);
        Log.Information("--------------------------------------------------------------------------------");

        IsHydrated = true;
    }
}
