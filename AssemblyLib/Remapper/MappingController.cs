using System.Diagnostics;
using System.Reflection;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AssemblyLib.Enums;
using AssemblyLib.Models;
using AssemblyLib.ReMapper.MetaData;
using AssemblyLib.Utils;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper;

[Injectable(InjectionType.Singleton)]
public class MappingController(
    DataProvider dataProvider,
    Statistics statistics, 
    Renamer renamer,
    Publicizer publicizer,
    TypeFilters typeFilters,
    AssemblyUtils assemblyUtils,
    AttributeFactory attributeFactory
    )
{
    private ModuleDefinition? Module { get; set; }
    private List<TypeDefinition> Types { get; set; } = [];
    
    private string OutPath { get; set; } = string.Empty;
    
    private readonly List<string> _alreadyGivenNames = [];
    private string _targetAssemblyPath = string.Empty;
    
    
    /// <summary>
    /// Start the remapping process
    /// </summary>
    public async Task Run(
        string targetAssemblyPath,
        string? oldAssemblyPath,
        string outPath = "",
        bool validate = false)
    {
        //Logger.Stopwatch.Start();
        OutPath = outPath;
        _targetAssemblyPath = targetAssemblyPath;
        
        Module = dataProvider.LoadModule(targetAssemblyPath);
        
        LoadOrDeobfuscateAssembly();

        if (!RunRemapProcess(validate))
        {
            statistics.DisplayStatistics(true);
        }
        
        // Don't go any further during a validation
        if (validate) return;

        await PublicizeAndFixAssembly(oldAssemblyPath);
        await StartWriteAssemblyTasks();
    }

    /// <summary>
    /// Load or Deobfuscate the assembly
    /// </summary>
    private void LoadOrDeobfuscateAssembly()
    {
        var result = assemblyUtils.TryDeObfuscate(
            Module, _targetAssemblyPath);
        
        _targetAssemblyPath = result.Item1;
        Module = result.Item2;
        
        Types.AddRange(Module.GetAllTypes());
    }
    
    /// <summary>
    /// Runs the matching process for remaps
    /// </summary>
    /// <param name="validate">Generates dynamic item.json remaps if false</param>
    /// <returns>Returns true if succeeded or false if not</returns>
    private bool RunRemapProcess(bool validate)
    {
        if (!validate)
        {
            GenerateDynamicRemaps(_targetAssemblyPath);
        }
        
        StartMatchingTasks();
        ChooseBestMatches();
        
        var succeeded = statistics.DisplayFailuresAndChanges(false, true);

        return succeeded;
    }

    /// <summary>
    /// Publicize, fix name mangled method names, create custom spt attr, and fix async attributes
    /// </summary>
    /// <param name="oldAssemblyPath">Old assembly path for use with spt attr</param>
    private async Task PublicizeAndFixAssembly(string? oldAssemblyPath)
    {
        PublicizeObfuscatedTypes();
        
        renamer.FixInterfaceMangledMethodNames(Module!);
        
        if (!string.IsNullOrEmpty(oldAssemblyPath))
        {
            await attributeFactory.CreateCustomTypeAttribute(Module!);
        }
        
        attributeFactory.UpdateAsyncAttributes(Module!);
    }
    
    #region Matching
    
    /// <summary>
    /// Queues the workload for finding best matches for a given remap.
    /// </summary>
    private void StartMatchingTasks()
    {
        Log.Information("Creating Mapping Table...");
        
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
        if (!typeFilters.DoesTypePassFilters(mapping, ref types)) return;
        
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
            Log.Error("Could not find type [{MappingOriginalTypeName}]", mapping.OriginalTypeName);
            return;
        }
        
        mapping.TypePrimeCandidate = type;
        mapping.OriginalTypeName = type.Name!;
        mapping.Succeeded = true;

        _alreadyGivenNames.Add(mapping.OriginalTypeName);

        Log.Warning("Forcing `{MappingOriginalTypeName}` to `{MappingNewTypeName}`", 
            mapping.OriginalTypeName, 
            mapping.NewTypeName
            );
        
        RenameAndPublicizeRemap(mapping);
    }
    
    /// <summary>
    /// Choose the best possible match from all remaps
    /// </summary>
    private void ChooseBestMatches()
    {
        Log.Information("Renaming and Publicizing Remaps...");
        
        foreach (var remap in DataProvider.Remaps)
        {
            if (remap.UseForceRename)
            {
                HandleForceRename(remap);
                continue;
            }
            
            if (remap.TypeCandidates.Count == 0 || remap.Succeeded) { return; }
            
            var winner = remap.TypeCandidates.FirstOrDefault();
            
            if (winner is null || IsAmbiguousMatch(remap, winner)) continue;
            
            remap.Succeeded = true;
            remap.OriginalTypeName = winner.Name!;
            
            Log.Information("Match [{RemapNewTypeName}] -> [{RemapOriginalTypeName}]", 
                remap.NewTypeName, 
                remap.OriginalTypeName
                );
            
            RenameAndPublicizeRemap(remap);
        }
    }

    /// <summary>
    /// Is this match ambiguous with a previous match?
    /// </summary>
    /// <param name="remap">Remap to check</param>
    /// <param name="match">Type definition to check</param>
    /// <returns>True if ambiguous match</returns>
    private bool IsAmbiguousMatch(RemapModel remap, TypeDefinition match)
    {
        remap.TypePrimeCandidate = match;
        remap.OriginalTypeName = match.Name!;
        
        if (_alreadyGivenNames.Contains(match.FullName))
        {
            remap.NoMatchReasons.Add(ENoMatchReason.AmbiguousWithPreviousMatch);
            remap.AmbiguousTypeMatch = match.FullName;
            remap.Succeeded = false;

            Log.Error("Failure During Matching: [{RemapNewTypeName}] is ambiguous with previous match", 
                remap.NewTypeName
                );
            
            return true;
        }

        _alreadyGivenNames.Add(remap.OriginalTypeName);
        return false;
    }

    #endregion
    
    /// <summary>
    /// Process the renaming and publication of a specific remap
    /// </summary>
    /// <param name="remap">Mapping to process</param>
    private void RenameAndPublicizeRemap(RemapModel remap)
    {
        renamer.RenameRemap(Module!, remap);
        
        var fieldsToFix = publicizer.PublicizeType(remap.TypePrimeCandidate!);
            
        if (fieldsToFix.Count == 0) return;
        
        FixPublicizedFieldNamesOnType(fieldsToFix);
    }

    private void PublicizeObfuscatedTypes()
    {
        Log.Information("Publicizing Obfuscated Types...");
        
        // Filter down remaining types to ones that we have not remapped.
        // We can use _alreadyGivenNames because it should contain all mapped classes at this point.
        var obfuscatedTypes = Types.Where(t => !_alreadyGivenNames.Contains(t.Name!));
        
        foreach (var type in obfuscatedTypes)
        {
            var fieldsToFix = publicizer.PublicizeType(type);
            
            if (fieldsToFix.Count == 0) continue;
            
            FixPublicizedFieldNamesOnType(fieldsToFix);
        }
    }

    private void FixPublicizedFieldNamesOnType(List<FieldDefinition> publicizedFields)
    {
        foreach (var field in publicizedFields)
        {
            renamer.RenamePublicizedFieldAndUpdateMemberRefs(Module!, field);
        }
    }
    
    /// <summary>
    /// Finds GClass associations from items.json based on parent types and mongoId
    /// </summary>
    /// <param name="path">Path to the cleaned assembly</param>
    private void GenerateDynamicRemaps(string path)
    {
        Log.Information("Generating Dynamic Remaps...");

        var templateMappingClass = GetTemplateMappingClass(path);

        if (templateMappingClass is null)
        {
            Log.Error("templateMappingClass is null...");
            return;
        }
        
        var typeTable = (Dictionary<string, Type>)templateMappingClass
            .GetField("TypeTable")!
            .GetValue(templateMappingClass)!;
        
        Log.Information("Overriding Item Classes...");
        
        BuildAssociationFromTable(typeTable, "ItemClass", true);
        
        var templateTypeTable = (Dictionary<string, Type>)templateMappingClass
            .GetField("TemplateTypeTable")!
            .GetValue(templateMappingClass)!;
        
        Log.Information("Overriding Template Classes...");
        
        BuildAssociationFromTable(templateTypeTable, "TemplateClass", false);
    }

    /// <summary>
    /// Gets the Template ID mapping class from the assembly as a type for reflection usage
    /// </summary>
    /// <param name="path">Path to load the assembly from</param>
    /// <returns>Template mapping class type or null</returns>
    private Type? GetTemplateMappingClass(string path)
    {
        // HACK: Because this is written in net8 and the assembly is net472 we must resolve the type this way instead of
        // filtering types directly using GetTypes() Otherwise, it causes serialization issues.
        // This is also necessary because we can't access non-compile time constants with dnlib.
        var templateMappingTypeDef = Types.SingleOrDefault(t => 
            t.Fields.Select(f => f.Name)
                .ToList()
                .Contains("TypeTable"));
        
        if (templateMappingTypeDef is null)
        {
            Log.Error("Could not find type for field TypeTable");
            return null;
        }

        if (!path.EndsWith("cleaned.dll"))
        {
            path = path.Replace(".dll", "-cleaned.dll");
        }
        
        var assembly = Assembly.LoadFrom(path);
        var templateMappingClass = assembly.Modules
            .First()
            .GetType(templateMappingTypeDef.Name!);
        
        if (templateMappingClass is null)
        {
            Log.Error("Could not resolve type for {Utf8String}", templateMappingTypeDef.Name);
            return null;
        }
        
        return templateMappingClass;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="table">Type or Template table</param>
    /// <param name="extName">ItemClass or TemplateClass</param>
    /// <param name="isItemClass">Is this table for items or templates?</param>
    private void BuildAssociationFromTable(Dictionary<string, Type> table, string extName, bool isItemClass)
    {
        foreach (var type in table)
        {
            var overrideTable = isItemClass
                ? dataProvider.Settings.ItemObjectIdOverrides
                : dataProvider.Settings.TemplateObjectIdOverrides;
            
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
            
            Log.Information("Overriding type {ValueName} to {RemapNewTypeName}", 
                type.Value.Name, 
                remap.NewTypeName
                );
            
            DataProvider.Remaps.Add(remap);
        }
    }
    
    /// <summary>
    /// Write the assembly back to disk and update the mapping file on disk
    /// </summary>
    private async Task StartWriteAssemblyTasks()
    {
        const string dllName = "-cleaned-remapped-publicized.dll";
        OutPath = Path.Combine(OutPath,  Module.Name!.Replace(".dll", dllName));

        try
        {
            Module.Assembly?.Write(OutPath);
            
            //Module!.Write(OutPath);
        }
        catch (Exception e)
        {
            Log.Error("Exception during write assembly task:\n{Exception}",e.Message);
            return;
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
            Log.Error("Exception during write hollow task:\n{Exception}",e.Message);
            return;
        }
        
        StartHDiffz();
        
        statistics.DisplayStatistics(false, hollowedPath, OutPath);
    }

    /// <summary>
    /// Hollows out all logic from the dll
    /// </summary>
    private async Task StartHollow()
    {
        Log.Information("Creating Hollow...");
        
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
                    Log.Error("Exception in task:\n{ExMessage}", ex.Message);
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
        Log.Information("Creating Delta...");
        
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
            Log.Error("Error: {Error}",error);
        }
    }
}