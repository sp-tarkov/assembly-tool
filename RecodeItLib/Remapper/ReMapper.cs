using dnlib.DotNet;
using dnlib.DotNet.Emit;
using ReCodeItLib.Enums;
using ReCodeItLib.Models;
using ReCodeItLib.Utils;
using System.Diagnostics;
using System.Reflection;

namespace ReCodeItLib.ReMapper;

public class ReMapper
{
    private ModuleDefMD? Module { get; set; }

    public static bool IsRunning { get; private set; } = false;
    
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
        string outPath = "",
        bool validate = false)
    {
        _remaps = [];
        _remaps = remapModels;
        _alreadyGivenNames = [];
        
        assemblyPath = AssemblyUtils.TryDeObfuscate(
            DataProvider.LoadModule(assemblyPath), 
            assemblyPath, 
            out var module);

        Module = module;
        
        OutPath = outPath;

        if (!Validate(_remaps)) return;

        IsRunning = true;
        Stopwatch.Start();
        
        var types = Module.GetTypes();

        if (!validate)
        {
            GenerateDynamicRemaps(assemblyPath, types);
        }
        
        FindBestMatches(types, validate);
        ChooseBestMatches();

        // Don't go any further during a validation
        if (validate)
        {
            new Statistics(_remaps, Stopwatch, OutPath)
                .DisplayStatistics(true);
            
            return;
        }
        
        RenameMatches(types);
        
        Publicize();
        
        // We are done, write the assembly
        WriteAssembly();
    }
    
    private void FindBestMatches(IEnumerable<TypeDef> types, bool validate)
    {
        Logger.LogSync("Finding Best Matches...", ConsoleColor.Green);
        
        var tasks = new List<Task>(_remaps.Count);
        foreach (var remap in _remaps)
        {
            tasks.Add(
                Task.Factory.StartNew(() =>
                {
                    ScoreMapping(remap, types);
                })
            );
        }

        if (!validate)
        {
            while (!tasks.TrueForAll(t => t.Status == TaskStatus.RanToCompletion))
            {
                Logger.DrawProgressBar(tasks.Where(t => t.IsCompleted)!.Count() + 1, tasks.Count, 50);
            }
        }
        
        Task.WaitAll(tasks.ToArray());
    }

    private void RenameMatches(IEnumerable<TypeDef> types)
    {
        Logger.LogSync("\nRenaming...", ConsoleColor.Green);
        
        var renameTasks = new List<Task>(_remaps.Count);
        foreach (var remap in _remaps)
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
            Logger.DrawProgressBar(renameTasks.Where(t => t.IsCompleted)!.Count() + 1, renameTasks.Count, 50);
        }
        
        Task.WaitAll(renameTasks.ToArray());
    }

    private void Publicize()
    {
        // Don't publicize and unseal until after the remapping, so we can use those as search parameters
        Logger.LogSync("\nPublicizing classes...", ConsoleColor.Green);

        SPTPublicizer.PublicizeClasses(Module);
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
        if (mapping.SearchParams.NestedTypes.IsNested is false)
        {
            types = types.Where(type => tokens!.Any(token => type.Name.StartsWith(token)));
        }

        if (mapping.SearchParams.NestedTypes.NestedTypeParentName != string.Empty)
        {
            types = types.Where(t => t.DeclaringType != null && t.DeclaringType.Name == mapping.SearchParams.NestedTypes.NestedTypeParentName);
        }
        
        // Run through a series of filters and report an error if all types are filtered out.
        var filters = new TypeFilters();
        
        if (!filters.DoesTypePassFilters(mapping, ref types)) return;
        
        mapping.TypeCandidates.UnionWith(types);
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

        var dllName = "-cleaned-remapped-publicized.dll";
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
}