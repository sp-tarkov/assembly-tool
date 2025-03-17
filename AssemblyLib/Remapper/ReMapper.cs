using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Diagnostics;
using System.Reflection;
using AssemblyLib.Application;
using AssemblyLib.Enums;
using AssemblyLib.Models;
using AssemblyLib.ReMapper.MetaData;
using AssemblyLib.Utils;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace AssemblyLib.ReMapper;

public class ReMapper(string targetAssemblyPath)
{
    private ModuleDefMD Module { get; set; } = DataProvider.LoadModule(targetAssemblyPath);
    private List<TypeDef> Types { get; set; } = [];
    
    private string OutPath { get; set; } = string.Empty;
    
    private readonly List<string> _alreadyGivenNames = [];
    private string _targetAssemblyPath = targetAssemblyPath;

    /// <summary>
    /// Start the remapping process
    /// </summary>
    public async Task InitializeRemap(
        string oldAssemblyPath,
        string outPath = "",
        bool validate = false)
    {
        Logger.Stopwatch.Start();
        
        _targetAssemblyPath = AssemblyUtils.TryDeObfuscate(
            Module, 
            _targetAssemblyPath, 
            out var module);

        Module = module;
        OutPath = outPath;
        
        Types.AddRange(Module.GetTypes());
        
        InitializeComponents(oldAssemblyPath);
        if (!validate)
        {
            GenerateDynamicRemaps(_targetAssemblyPath);
        }
        
        await StartMatchingTasks(validate);

        // Don't go any further during a validation
        if (validate)
        {
            Context.Instance.Get<Statistics>()
                !.DisplayStatistics(true);
            
            return;
        }
        
        await Context.Instance.Get<Renamer>()!.StartRenameProcess();
        await Context.Instance.Get<Publicizer>()!.StartPublicizeTypesTask();
        
        if (!string.IsNullOrEmpty(oldAssemblyPath))
        {
            await Context.Instance.Get<AttributeFactory>()
                !.CreateCustomTypeAttribute();
        }
        
        await StartWriteAssemblyTasks();
    }

    private void InitializeComponents(string oldAssemblyPath)
    {
        var ctx = Context.Instance;

        var stats = new Statistics();
        var renamer = new Renamer(Types, stats);
        var publicizer = new Publicizer(Types, stats);
        var attrFactory = new AttributeFactory(Module, Types);
        
        ctx.RegisterComponent<Statistics>(stats);
        ctx.RegisterComponent<Renamer>(renamer);
        ctx.RegisterComponent<Publicizer>(publicizer);
        ctx.RegisterComponent<AttributeFactory>(attrFactory);
        
        if (!string.IsNullOrEmpty(oldAssemblyPath))
        {
            var diff = new DiffCompare(DataProvider.LoadModule(oldAssemblyPath));
            ctx.RegisterComponent<DiffCompare>(diff);
        }
    }
    
    #region Matching
    
    private async Task StartMatchingTasks(bool validate)
    {
        var tasks = new List<Task>(DataProvider.Remaps.Count);
        foreach (var remap in DataProvider.Remaps)
        {
            tasks.Add(
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        MatchRemap(remap);
                    }
                    catch (Exception ex)
                    {
                        Logger.QueueTaskException($"Exception in task: {ex.Message}");
                    }
                })
            );
        }

        if (!validate)
        {
            await Logger.DrawProgressBar(tasks, "Finding Best Matches");
        }
        else
        {
            await Task.WhenAll(tasks.ToArray());
        }
        
        ChooseBestMatches();
    }
    
    /// <summary>
    /// First we filter our type collection based on simple search parameters (true/false/null)
    /// where null is a third disabled state. Then we score the types based on the search parameters
    /// </summary>
    /// <param name="mapping">Mapping to score</param>
    /// <param name="types">Types to filter</param>
    private void MatchRemap(RemapModel mapping)
    {
        var tokens = DataProvider.Settings.TypeNamesToMatch;

        if (mapping.UseForceRename)
        {
            HandleDirectRename(mapping);
            return;
        }

        // Filter down nested objects
        var types = !mapping.SearchParams.NestedTypes.IsNested
            ? Types.Where(type => tokens.Any(token => type.Name.StartsWith(token)))
            : Types.Where(t => t.DeclaringType != null);

        if (mapping.SearchParams.NestedTypes.NestedTypeParentName != string.Empty)
        {
            types = types.Where(t => t.DeclaringType.Name == mapping.SearchParams.NestedTypes.NestedTypeParentName);
        }
        
        // Run through a series of filters and report an error if all types are filtered out.
        var filters = new TypeFilters();
        
        if (!filters.DoesTypePassFilters(mapping, ref types)) return;
        
        mapping.TypeCandidates.UnionWith(types);
    }
    
    private void HandleDirectRename(RemapModel mapping)
    {
        foreach (var type in Types)
        {
            if (type.Name != mapping.OriginalTypeName) continue;
            
            mapping.TypePrimeCandidate = type;
            mapping.OriginalTypeName = type.Name.String;
            mapping.Succeeded = true;

            _alreadyGivenNames.Add(mapping.OriginalTypeName);

            return;
        }
    }
    
    /// <summary>
    /// Choose the best possible match from all remaps
    /// </summary>
    private void ChooseBestMatches()
    {
        foreach (var remap in DataProvider.Remaps)
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
    
    #endregion
    
    private void GenerateDynamicRemaps(string path)
    {
        // HACK: Because this is written in net8 and the assembly is net472 we must resolve the type this way instead of
        // filtering types directly using GetTypes() Otherwise, it causes serialization issues.
        // This is also necessary because we can't access non-compile time constants with dnlib.
        var templateMappingTypeDef = Types.SingleOrDefault(t => t.FindField("TypeTable") != null);
        
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
            .GetField("TypeTable")!
            .GetValue(templateMappingClass)!;
        
        BuildAssociationFromTable(typeTable, "ItemClass", true);
        
        var templateTypeTable = (Dictionary<string, Type>)templateMappingClass
            .GetField("TemplateTypeTable")!
            .GetValue(templateMappingClass)!;
        
        BuildAssociationFromTable(templateTypeTable, "TemplateClass", false);
    }
    
    private void BuildAssociationFromTable(Dictionary<string, Type> table, string extName, bool isItemClass)
    {
        foreach (var type in table)
        {
            var overrideTable = isItemClass
                ? DataProvider.Settings.ItemObjectIdOverrides
                : DataProvider.Settings.TemplateObjectIdOverrides;
            
            if (!DataProvider.ItemTemplates.TryGetValue(type.Key, out var template) ||
                !type.Value.Name.StartsWith("GClass"))
            {
                continue;
            }
            
            var remap = new RemapModel
            {
                OriginalTypeName = type.Value.Name,
                NewTypeName = $"{template.Name}{extName}",
                UseForceRename = true
            };

            if (overrideTable.TryGetValue(type.Key, out var overriddenTypeName))
            {
                remap.NewTypeName = overriddenTypeName;
            }
            
            DataProvider.Remaps.Add(remap);
        }
    }
    
    /// <summary>
    /// Write the assembly back to disk and update the mapping file on disk
    /// </summary>
    private async Task StartWriteAssemblyTasks()
    {
        const string dllName = "-cleaned-remapped-publicized.dll";
        OutPath = Path.Combine(OutPath,  Module?.Name?.Replace(".dll", dllName));

        try
        {
            Module!.Write(OutPath);
        }
        catch (Exception e)
        {
            Logger.Log(e);
            throw;
        }
        
        await StartHollow();

        var hollowedDir = Path.GetDirectoryName(OutPath);
        var hollowedPath = Path.Combine(hollowedDir!, "Assembly-CSharp-hollowed.dll");

        try
        {
            Module.Write(hollowedPath);
        }
        catch (Exception e)
        {
            Logger.Log(e);
            throw;
        }
        
        Context.Instance.Get<Statistics>()!
            .DisplayStatistics(false, hollowedPath, OutPath);
    }

    /// <summary>
    /// Hollows out all logic from the dll
    /// </summary>
    private async Task StartHollow()
    {
        var tasks = new List<Task>(Module!.GetTypes().Count());
        
        var body = new CilBody();
        body.Instructions.Add(OpCodes.Ret.ToInstruction());
        foreach (var type in Types)
        {
            tasks.Add(
                Task.Factory.StartNew(() =>
            {
                try
                {
                    HollowType(type, body);
                }
                catch (Exception ex)
                {
                    Logger.QueueTaskException($"Exception in task: {ex.Message}");
                }
            }));
        }
        
        await Logger.DrawProgressBar(tasks, "Hollowing Types");
    }

    private static void HollowType(TypeDef type, CilBody body)
    {
        foreach (var method in type.Methods.Where(m => m.HasBody))
        {
            method.Body = body;
        }
    }
}