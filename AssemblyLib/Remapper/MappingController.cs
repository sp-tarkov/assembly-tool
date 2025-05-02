using System.Diagnostics;
using System.Reflection;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Builder;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Builder;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AssemblyLib.Application;
using AssemblyLib.Enums;
using AssemblyLib.Models;
using AssemblyLib.ReMapper.MetaData;
using AssemblyLib.Utils;
using FieldAttributes = AsmResolver.PE.DotNet.Metadata.Tables.Rows.FieldAttributes;

namespace AssemblyLib.ReMapper;

public class MappingController(string targetAssemblyPath)
{
    private ModuleDefinition Module { get; set; } = DataProvider.LoadModule(targetAssemblyPath);
    private List<TypeDefinition> Types { get; set; } = [];

    private List<TypeDefinition> _typesProcessed = [];
    
    private string OutPath { get; set; } = string.Empty;
    
    private readonly List<string> _alreadyGivenNames = [];
    private string _targetAssemblyPath = targetAssemblyPath;
    
    
    /// <summary>
    /// Start the remapping process
    /// </summary>
    public async Task Run(
        string oldAssemblyPath,
        string outPath = "",
        bool validate = false)
    {
        Logger.Stopwatch.Start();
        
        var result = AssemblyUtils.TryDeObfuscate(
            Module, _targetAssemblyPath);

        OutPath = outPath;
        _targetAssemblyPath = result.Item1;
        Module = result.Item2;
        
        Types.AddRange(Module.GetAllTypes());
        
        InitializeComponents(oldAssemblyPath);
        if (!validate)
        {
            GenerateDynamicRemaps(_targetAssemblyPath);
        }
        
        StartMatchingTasks();
        ChooseBestMatches();
        
        var succeeded = Context.Instance.Get<Statistics>()
            !.DisplayFailuresAndChanges(false, true);
        
        // Don't go any further during a validation
        if (validate || !succeeded)
        {
            Context.Instance.Get<Statistics>()
                !.DisplayStatistics(true);
            
            return;
        }
        
        PublicizeObfuscatedTypes();
        
        Context.Instance.Get<Renamer>()!
            .FixInterfaceMangledMethodNames();
        
        if (!string.IsNullOrEmpty(oldAssemblyPath))
        {
            await Context.Instance.Get<AttributeFactory>()
                !.CreateCustomTypeAttribute();
        }
        
        Context.Instance.Get<AttributeFactory>()!
            .UpdateAsyncAttributes();
        
        await StartWriteAssemblyTasks();
    }

    /// <summary>
    /// Register Mapping dependencies with the app context
    /// </summary>
    /// <param name="oldAssemblyPath">Path to previous clients assembly</param>
    private void InitializeComponents(string oldAssemblyPath)
    {
        var ctx = Context.Instance;

        var stats = new Statistics();
        var renamer = new Renamer(Module, Types, stats);
        var publicizer = new Publicizer(stats);
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
    
    /// <summary>
    /// Queues the workload for finding best matches for a given remap.
    /// </summary>
    /// <param name="validate">Are we only validating, used for the automatcher</param>
    private void StartMatchingTasks()
    {
        Logger.Log("Creating Mapping Table...");
        
        foreach (var remap in DataProvider.Remaps)
        {
            MatchRemap(remap);
        }
    }
    
    /// <summary>
    /// First we filter our type collection based on simple search parameters (true/false/null)
    /// where null is a third disabled state. Then we score the types based on the search parameters
    /// </summary>
    /// <param name="mapping">Mapping to score</param>
    /// <param name="types">Types to filter</param>
    private void MatchRemap(RemapModel mapping)
    {
        if (mapping.UseForceRename) return;

        // Filter down nested objects
        var types = mapping.SearchParams.NestedTypes.IsNested
            ? Types.Where(t => t.IsNested)
            : Types.Where(t => (bool)t.Name?.IsObfuscatedName());
            

        if (mapping.SearchParams.NestedTypes.NestedTypeParentName != string.Empty)
        {
            types = types.Where(t => t.DeclaringType!.Name == mapping.SearchParams.NestedTypes.NestedTypeParentName);
        }
        
        // Run through a series of filters and report an error if all types are filtered out.
        var filters = new TypeFilters();
        
        if (!filters.DoesTypePassFilters(mapping, ref types)) return;
        
        mapping.TypeCandidates.UnionWith(types);
    }
    
    /// <summary>
    /// Directly renames a type instead of passing the remap through filters
    /// used for remaps generated from items.json (Dynamic remaps)
    /// </summary>
    /// <param name="mapping">Mapping to force rename</param>
    private void HandleForceRename(RemapModel mapping)
    {
        var type = Types
            .FirstOrDefault(t => t.Name == mapping.OriginalTypeName);

        if (type is null)
        {
            Logger.Log($"Could not find type [{mapping.OriginalTypeName}]", ConsoleColor.Red);
            return;
        }
        
        mapping.TypePrimeCandidate = type;
        mapping.OriginalTypeName = type.Name;
        mapping.Succeeded = true;

        _alreadyGivenNames.Add(mapping.OriginalTypeName);

        Logger.Log($"Forcing `{mapping.OriginalTypeName}` to `{mapping.NewTypeName}`", ConsoleColor.Yellow);
        RenameAndPublicizeRemap(mapping);
    }
    
    /// <summary>
    /// Choose the best possible match from all remaps
    /// </summary>
    private void ChooseBestMatches()
    {
        Logger.Log("Renaming and Publicizing Remaps...");
        
        foreach (var remap in DataProvider.Remaps)
        {
            if (remap.UseForceRename)
            {
                HandleForceRename(remap);
                continue;
            }
            
            if (remap.TypeCandidates.Count == 0 || remap.Succeeded) { return; }
            
            var winner = remap.TypeCandidates.FirstOrDefault();
        
            if (winner is null) { return; }
        
            remap.TypePrimeCandidate = winner;
            remap.OriginalTypeName = winner.Name!;
        
            if (_alreadyGivenNames.Contains(winner.FullName))
            {
                remap.NoMatchReasons.Add(ENoMatchReason.AmbiguousWithPreviousMatch);
                remap.AmbiguousTypeMatch = winner.FullName;
                remap.Succeeded = false;

                Logger.Log($"Failure During Matching: [{remap.NewTypeName}] is ambiguous with previous match", ConsoleColor.Red);
                return;
            }
            
            _alreadyGivenNames.Add(remap.OriginalTypeName);

            remap.Succeeded = true;

            remap.OriginalTypeName = winner.Name!;
            
            Logger.Log($"Match [{remap.NewTypeName}] -> [{remap.OriginalTypeName}]", ConsoleColor.Green);
            RenameAndPublicizeRemap(remap);
        }
    }

    #endregion
    
    /// <summary>
    /// Process the renaming and publication of a specific remap
    /// </summary>
    /// <param name="remap">Mapping to process</param>
    private void RenameAndPublicizeRemap(RemapModel remap)
    {
        var renamer = Context.Instance.Get<Renamer>()!;
        var publicizer = Context.Instance.Get<Publicizer>()!;
        
        renamer.RenameRemap(remap);
        
        var fieldsToFix = publicizer.PublicizeType(remap.TypePrimeCandidate!);
            
        if (fieldsToFix.Count == 0) return;
        
        FixPublicizedFieldNamesOnType(fieldsToFix);
        
        _typesProcessed.Add(remap.TypePrimeCandidate!);
    }

    private void PublicizeObfuscatedTypes()
    {
        Logger.Log("Publicizing Obfuscated Types...");
        
        // Filter down remaining types to ones that we have not remapped.
        // We can use _alreadyGivenNames because it should contain all mapped classes at this point.
        var obfuscatedTypes = Types.Where(t => !_alreadyGivenNames.Contains(t.Name!));
        var publicizer = Context.Instance.Get<Publicizer>()!;
        
        foreach (var type in obfuscatedTypes)
        {
            var fieldsToFix = publicizer.PublicizeType(type);
            
            if (fieldsToFix.Count == 0) continue;
            
            FixPublicizedFieldNamesOnType(fieldsToFix);
        }
    }

    private static void FixPublicizedFieldNamesOnType(List<FieldDefinition> publicizedFields)
    {
        var renamer = Context.Instance.Get<Renamer>()!;
        
        foreach (var field in publicizedFields)
        {
            renamer.RenamePublicizedFieldAndUpdateMemberRefs(field);
        }
    }
    
    /// <summary>
    /// Finds GClass associations from items.json based on parent types and mongoId
    /// </summary>
    /// <param name="path">Path to the cleaned assembly</param>
    private void GenerateDynamicRemaps(string path)
    {
        Logger.Log("Generating Dynamic Remaps...");
        
        // HACK: Because this is written in net8 and the assembly is net472 we must resolve the type this way instead of
        // filtering types directly using GetTypes() Otherwise, it causes serialization issues.
        // This is also necessary because we can't access non-compile time constants with dnlib.
        var templateMappingTypeDef = Types.SingleOrDefault(t => 
            t.Fields.Select(f => f.Name)
                .ToList()
                .Contains("TypeTable"));
        
        if (templateMappingTypeDef is null)
        {
            Logger.Log("Could not find type for field TypeTable", ConsoleColor.Red);
            return;
        }

        if (!path.EndsWith("cleaned.dll"))
        {
            path = path.Replace(".dll", "-cleaned.dll");
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
        
        Logger.Log("Overriding Item Classes...");
        
        BuildAssociationFromTable(typeTable, "ItemClass", true);
        
        var templateTypeTable = (Dictionary<string, Type>)templateMappingClass
            .GetField("TemplateTypeTable")!
            .GetValue(templateMappingClass)!;
        
        Logger.Log("Overriding Template Classes...");
        
        BuildAssociationFromTable(templateTypeTable, "TemplateClass", false);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="table">Type or Template table</param>
    /// <param name="extName">ItemClass or TemplateClass</param>
    /// <param name="isItemClass">Is this table for items or templates?</param>
    private static void BuildAssociationFromTable(Dictionary<string, Type> table, string extName, bool isItemClass)
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

            switch (template.Name)
            {
                // Solves a duplication assign of key types to the same GClass (Wtf bsg)
                case "KeyMechanical" when extName == "TemplateClass":
                case "BuiltInInserts" when extName == "TemplateClass":
                    continue;
            }

            var remap = new RemapModel
            {
                OriginalTypeName = type.Value.Name,
                NewTypeName = $"{template.Name}{extName}",
                UseForceRename = true,
                Succeeded = true
            };

            if (overrideTable.TryGetValue(type.Key, out var overriddenTypeName))
            {
                remap.NewTypeName = overriddenTypeName;
            }
            
            Logger.Log($"Overriding type {type.Value.Name} to {remap.NewTypeName}", ConsoleColor.Green);
            
            DataProvider.Remaps.Add(remap);
        }
    }
    
    /// <summary>
    /// Write the assembly back to disk and update the mapping file on disk
    /// </summary>
    private async Task StartWriteAssemblyTasks()
    {
        const string dllName = "-cleaned-remapped-publicized.dll";
        OutPath = Path.Combine(OutPath,  Module.Name!.ToString().Replace(".dll", dllName));

        try
        {
            Module.Assembly?.Write(OutPath);
            
            //Module!.Write(OutPath);
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
        
        StartHDiffz();
        
        Context.Instance.Get<Statistics>()!
            .DisplayStatistics(false, hollowedPath, OutPath);
    }

    /// <summary>
    /// Hollows out all logic from the dll
    /// </summary>
    private async Task StartHollow()
    {
        Logger.Log("Creating Hollow...");
        
        var tasks = new List<Task>(Types.Count());
        
        foreach (var type in Types)
        {
            tasks.Add(
                Task.Factory.StartNew(() =>
            {
                try
                {
                    HollowType(type);
                }
                catch (Exception ex)
                {
                    Logger.QueueTaskException($"Exception in task: {ex.Message}");
                }
            }));
        }
        
        await Task.WhenAll(tasks.ToArray());
    }

    private static void HollowType(TypeDefinition type)
    {
        foreach (var method in type.Methods.Where(m => m.HasMethodBody))
        {
            // Create a new empty CIL body
            var newBody = new CilMethodBody(method);

            // If the method returns something, return default value
            if (method.Signature?.ReturnType != null && method.Signature.ReturnType.ElementType != ElementType.Void)
            {
                // Push default value onto the stack
                newBody.Instructions.Add(CilOpCodes.Ldnull);
            }

            // Just return (for void methods)
            newBody.Instructions.Add(CilOpCodes.Ret);

            // Assign the new method body
            method.CilMethodBody = newBody;
        }
    }
    
    private void StartHDiffz()
    {
        Logger.Log("Creating Delta...");
        
        var hdiffPath = Path.Combine(AppContext.BaseDirectory, "Data", "hdiffz.exe");

        var outDir = Path.GetDirectoryName(OutPath);
        
        var originalFile = Path.Combine(outDir!, "Assembly-CSharp.dll");
        var patchedFile = Path.Combine(outDir!, "Assembly-CSharp-cleaned-remapped-publicized.dll");
        var deltaFile = Path.Combine(outDir!, "Assembly-CSharp.dll.delta");

        if (File.Exists(deltaFile))
        {
            File.Delete(deltaFile);
        }
        
        var arguments = $"-s-64 -c-zstd-21-24 -d \"{originalFile}\" \"{patchedFile}\" \"{deltaFile}\"";
        
        var startInfo = new ProcessStartInfo
        {
            FileName = hdiffPath,
            WorkingDirectory = Path.GetDirectoryName(hdiffPath),
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;

        process.Start();
        //var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (error.Length > 0)
        {
            Logger.Log("Error: " + error, ConsoleColor.Red);
        }
    }
}