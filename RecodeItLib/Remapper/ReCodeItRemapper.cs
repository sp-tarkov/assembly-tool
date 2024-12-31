using dnlib.DotNet;
using dnlib.DotNet.Emit;
using ReCodeItLib.Enums;
using ReCodeItLib.Models;
using ReCodeItLib.ReMapper.Search;
using ReCodeItLib.Utils;
using System.Diagnostics;
using System.Reflection;

namespace ReCodeItLib.ReMapper;

public class ReCodeItRemapper
{
    private ModuleDefMD? Module { get; set; }

    public static bool IsRunning { get; private set; } = false;

    public delegate void OnCompleteHandler();

    public event OnCompleteHandler? OnComplete;

    private static readonly Stopwatch Stopwatch = new();

    private RemapperSettings? Settings => DataProvider.Settings?.Remapper;

    private string OutPath { get; set; } = string.Empty;
    
    private List<RemapModel> _remaps = [];

    private List<string> _alreadyGivenNames = [];

    /// <summary>
    /// Start the remapping process
    /// </summary>
    public void InitializeRemap(
        List<RemapModel> remapModels,
        string assemblyPath,
        string outPath,
        bool validate = false)
    {
        _remaps = [];
        _remaps = remapModels;
        _alreadyGivenNames = [];
        
        Module = DataProvider.LoadModule(assemblyPath);
        
        OutPath = outPath;

        if (!Validate(_remaps)) return;

        IsRunning = true;
        Stopwatch.Start();
        
        var types = Module.GetTypes();
        
        if (!types.Any(t => t.Name.Contains("GClass")))
        {
            Logger.Log("Assembly is obfuscated, running de-obfuscation...\n", ConsoleColor.Yellow);
            
            Module.Dispose();
            Module = null;
            
            Deobfuscator.Deobfuscate(assemblyPath);
            
            var cleanedName = Path.GetFileNameWithoutExtension(assemblyPath);
            cleanedName = $"{cleanedName}-cleaned.dll";
            
            var newPath = Path.GetDirectoryName(assemblyPath);
            newPath = Path.Combine(newPath!, cleanedName);
            
            Console.WriteLine($"Cleaning assembly: {newPath}");
            
            Module = DataProvider.LoadModule(newPath);
            types = Module.GetTypes();
            
            GenerateDynamicRemaps(newPath, types);
        }
        else
        {
            GenerateDynamicRemaps(assemblyPath, types);
        }
        
        Logger.LogSync("Finding Best Matches...", ConsoleColor.Green);
        
        var tasks = new List<Task>(remapModels.Count);
        foreach (var remap in remapModels)
        {
            tasks.Add(
                Task.Factory.StartNew(() =>
                {
                    ScoreMapping(remap, types);
                })
            );
        }
        
        while (!tasks.TrueForAll(t => t.Status == TaskStatus.RanToCompletion))
        {
            Logger.DrawProgressBar(tasks.Where(t => t.IsCompleted)!.Count(), tasks.Count, 50);
        }
        
        Task.WaitAll(tasks.ToArray());

        ChooseBestMatches();

        // Don't go any further during a validation
        if (validate)
        {
            new Statistics(_remaps, Stopwatch, OutPath)
                .DisplayStatistics(true);
            
            return;
        }

        Logger.LogSync("\nRenaming...", ConsoleColor.Green);
        
        var renameTasks = new List<Task>(remapModels.Count);
        foreach (var remap in remapModels)
        {
            renameTasks.Add(
                Task.Factory.StartNew(() =>
                {
                    RenameHelper.RenameAll(types, remap);
                })
            );
        }
        
        while (!renameTasks.TrueForAll(t => t.Status == TaskStatus.RanToCompletion))
        {
            Logger.DrawProgressBar(renameTasks.Where(t => t.IsCompleted)!.Count(), tasks.Count - 1, 50);
        }
        
        Task.WaitAll(renameTasks.ToArray());
        
        // Don't publicize and unseal until after the remapping, so we can use those as search parameters
        if (Settings!.MappingSettings!.Publicize)
        {
            Logger.LogSync("\nPublicizing classes...", ConsoleColor.Green);

            SPTPublicizer.PublicizeClasses(Module);
        }
        
        // We are done, write the assembly
        WriteAssembly();
    }

    private bool Validate(List<RemapModel> remaps)
    {
        var duplicateGroups = remaps
            .GroupBy(m => m.NewTypeName)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicateGroups.Count() > 1)
        {
            Logger.Log($"There were {duplicateGroups.Count()} duplicated sets of remaps.", ConsoleColor.Yellow);

            foreach (var duplicate in duplicateGroups)
            {
                var duplicateNewTypeName = duplicate.Key;
                Logger.Log($"Ambiguous NewTypeName: {duplicateNewTypeName} found. Cancelling Remap.", ConsoleColor.Red);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// First we filter our type collection based on simple search parameters (true/false/null)
    /// where null is a third disabled state. Then we score the types based on the search parameters
    /// </summary>
    /// <param name="mapping">Mapping to score</param>
    private void ScoreMapping(RemapModel mapping, IEnumerable<TypeDef> types)
    {
        var tokens = DataProvider.Settings?.Remapper?.TokensToMatch;

        if (mapping.UseForceRename)
        {
            HandleDirectRename(mapping, ref types);
            return;
        }

        // Filter down nested objects
        if (mapping.SearchParams.IsNested is false or null)
        {
            types = types.Where(type => tokens!.Any(token => type.Name.StartsWith(token)));
        }
        
        // Run through a series of filters and report an error if all types are filtered out.
        
        if (!FilterTypesByGeneric(mapping, ref types)) return;
        if (!FilterTypesByMethods(mapping, ref types)) return;
        if (!FilterTypesByFields(mapping, ref types)) return;
        if (!FilterTypesByProps(mapping, ref types)) return;
        if (!FilterTypesByEvents(mapping, ref types)) return;
        if (!FilterTypesByNested(mapping, ref types)) return;
        
        types = CtorTypeFilters.FilterByParameterCount(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.ConstructorParameterCount);
            mapping.TypeCandidates.UnionWith(types);
            return;
        }
        
        mapping.TypeCandidates.UnionWith(types);
    }

    private static bool FilterTypesByGeneric(RemapModel mapping, ref IEnumerable<TypeDef> types)
    {
        types = GenericTypeFilters.FilterPublic(types, mapping.SearchParams);

        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.IsPublic);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterAbstract(types, mapping.SearchParams);
        if (types.Count() == 1) return true;
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.IsPublic);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterSealed(types, mapping.SearchParams);
        if (types.Count() == 1) return true;
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.IsSealed);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterInterface(types, mapping.SearchParams);
        if (types.Count() == 1) return true;
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.IsInterface);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterStruct(types, mapping.SearchParams);
        if (types.Count() == 1) return true;
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.IsStruct);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterEnum(types, mapping.SearchParams);
        if (types.Count() == 1) return true;
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.IsEnum);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterAttributes(types, mapping.SearchParams);
        if (types.Count() == 1) return true;
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.HasAttribute);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterDerived(types, mapping.SearchParams);
        if (types.Count() == 1) return true;
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.IsDerived);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = GenericTypeFilters.FilterByGenericParameters(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.HasGenericParameters);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

        return true;
    }

    private static bool FilterTypesByMethods(RemapModel mapping, ref IEnumerable<TypeDef> types)
    {
        types = MethodTypeFilters.FilterByInclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.MethodsInclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = MethodTypeFilters.FilterByExclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.MethodsExclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = MethodTypeFilters.FilterByCount(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.MethodsCount);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

        return true;
    }

    private static bool FilterTypesByFields(RemapModel mapping, ref IEnumerable<TypeDef> types)
    {
        types = FieldTypeFilters.FilterByInclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.FieldsInclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = FieldTypeFilters.FilterByExclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.FieldsExclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = FieldTypeFilters.FilterByCount(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.FieldsCount);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

        return true;
    }

    private static bool FilterTypesByProps(RemapModel mapping, ref IEnumerable<TypeDef> types)
    {
        types = PropertyTypeFilters.FilterByInclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.PropertiesInclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = PropertyTypeFilters.FilterByExclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.PropertiesExclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = PropertyTypeFilters.FilterByCount(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.PropertiesCount);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

        return true;
    }

    private static bool FilterTypesByNested(RemapModel mapping, ref IEnumerable<TypeDef> types)
    {
        types = NestedTypeFilters.FilterByInclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.NestedTypeInclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = NestedTypeFilters.FilterByExclude(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.NestedTypeExclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        types = NestedTypeFilters.FilterByCount(types, mapping.SearchParams);
        
        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.NestedTypeCount);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }
        
        return true;
    }

    private static bool FilterTypesByEvents(RemapModel mapping, ref IEnumerable<TypeDef> types)
    {
        types = EventTypeFilters.FilterByInclude(types, mapping.SearchParams);

        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.EventsInclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

        types = EventTypeFilters.FilterByExclude(types, mapping.SearchParams);

        if (!types.Any())
        {
            AllTypesFilteredOutFor(mapping, ENoMatchReason.EventsExclude);
            mapping.TypeCandidates.UnionWith(types);
            return false;
        }

        return true;
    }

    private void HandleDirectRename(RemapModel mapping, ref IEnumerable<TypeDef> types)
    {
        foreach (var type in types)
        {
            if (type.Name == mapping.OriginalTypeName)
            {
                mapping.TypePrimeCandidate = type;
                mapping.OriginalTypeName = type.Name.String;
                mapping.Succeeded = true;

                _alreadyGivenNames.Add(mapping.OriginalTypeName);

                return;
            }
        }
    }

    private void GenerateDynamicRemaps(string path, IEnumerable<TypeDef> types)
    {
        // HACK: Because this is written in net8 and the assembly is net472 we must resolve the type this way instead of
        // filtering types directly using GetTypes() Otherwise, it causes serialization issues.
        // This is also necessary because we can't access non-compile time constants with dnlib.
        var templateMappingTypeDef = types.SingleOrDefault(t => t.FindField("TypeTable") != null);
        
        if (templateMappingTypeDef is null)
        {
            Logger.Log("Could not find type for field TypeTable", ConsoleColor.Red);
            return;
        }
        
        var assembly = Assembly.LoadFrom(path);
        var templateMappingClass = assembly.Modules
            .First()
            .GetType(templateMappingTypeDef.Name);
        
        if (templateMappingClass is null)
        {
            Logger.Log($"Could not resolve type for {templateMappingTypeDef.Name}", ConsoleColor.Red);
            return;
        }
        
        var typeTable = (Dictionary<string, Type>)templateMappingClass
            .GetField("TypeTable")
            .GetValue(templateMappingClass);
        
        BuildAssociationFromTable(typeTable!, "ItemClass");
        
        var templateTypeTable = (Dictionary<string, Type>)templateMappingClass
            .GetField("TemplateTypeTable")
            .GetValue(templateMappingClass);
        
        BuildAssociationFromTable(templateTypeTable!, "TemplateClass");
    }
    
    private void BuildAssociationFromTable(Dictionary<string, Type> table, string extName)
    {
        foreach (var type in table)
        {
            if (!DataProvider.ItemTemplates!.TryGetValue(type.Key, out var template) ||
                !type.Value.Name.StartsWith("GClass"))
            {
                continue;
            }
            
            var remap = new RemapModel
            {
                OriginalTypeName = type.Value.Name,
                NewTypeName = $"{template._name}{extName}",
                UseForceRename = true
            };
                
            _remaps.Add(remap);
        }
    }
    
    /// <summary>
    /// Choose the best possible match from all remaps
    /// </summary>
    private void ChooseBestMatches()
    {
        foreach (var remap in _remaps)
        {
            ChooseBestMatch(remap);
        }
    }

    /// <summary>
    /// Choose best match from a collection of types on a remap
    /// </summary>
    /// <param name="remap"></param>
    private void ChooseBestMatch(RemapModel remap)
    {
        if (remap.TypeCandidates.Count == 0 || remap.Succeeded) { return; }

        var winner = remap.TypeCandidates.FirstOrDefault();
        
        if (winner is null) { return; }
        
        remap.TypePrimeCandidate = winner;
        remap.OriginalTypeName = winner.Name.String;
        
        if (_alreadyGivenNames.Contains(winner.FullName))
        {
            remap.NoMatchReasons.Add(ENoMatchReason.AmbiguousWithPreviousMatch);
            remap.AmbiguousTypeMatch = winner.FullName;
            remap.Succeeded = false;

            return;
        }

        _alreadyGivenNames.Add(remap.OriginalTypeName);

        remap.Succeeded = true;

        remap.OriginalTypeName = winner.Name.String;
    }

    /// <summary>
    /// Write the assembly back to disk and update the mapping file on disk
    /// </summary>
    private void WriteAssembly()
    {
        var moduleName = Module?.Name;

        var dllName = "-cleaned-remapped.dll";
        if (Settings!.MappingSettings!.Publicize)
        {
            dllName = "-cleaned-remapped-publicized.dll";
        }
        OutPath = Path.Combine(OutPath, moduleName?.Replace(".dll", dllName));

        try
        {
            Module!.Write(OutPath);
        }
        catch (Exception e)
        {
            Logger.LogSync(e);
            throw;
        }

        Logger.LogSync("\nCreating Hollow...", ConsoleColor.Green);
        Hollow();

        var hollowedDir = Path.GetDirectoryName(OutPath);
        var hollowedPath = Path.Combine(hollowedDir!, "Assembly-CSharp-hollowed.dll");

        try
        {
            Module.Write(hollowedPath);
        }
        catch (Exception e)
        {
            Logger.LogSync(e);
            throw;
        }
        
        if (DataProvider.Settings?.Remapper?.MappingPath != string.Empty)
        {
            DataProvider.UpdateMapping(DataProvider.Settings!.Remapper!.MappingPath.Replace("mappings.", "mappings-new."), _remaps);
        }

        new Statistics(_remaps, Stopwatch, OutPath, hollowedPath)
            .DisplayStatistics();
        
        Stopwatch.Reset();
        Module = null;

        IsRunning = false;
        OnComplete?.Invoke();
    }

    /// <summary>
    /// Hollows out all logic from the dll
    /// </summary>
    private void Hollow()
    {
        foreach (var type in Module!.GetTypes())
        {
            foreach (var method in type.Methods.Where(m => m.HasBody))
            {
                if (!method.HasBody) continue;

                method.Body = new CilBody();
                method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            }
        }
    }
    
    /// <summary>
    /// This is used to log that all types for a given remap were filtered out.
    /// </summary>
    /// <param name="remap">remap model that failed</param>
    /// <param name="noMatchReason">Reason for filtering</param>
    private static void AllTypesFilteredOutFor(RemapModel remap, ENoMatchReason noMatchReason)
    {
        Logger.Log($"All types filtered out after `{noMatchReason}` filter for: `{remap.NewTypeName}`", ConsoleColor.Red);
    }
}