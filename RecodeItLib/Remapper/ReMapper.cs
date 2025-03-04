using dnlib.DotNet;
using dnlib.DotNet.Emit;
using ReCodeItLib.Enums;
using ReCodeItLib.Models;
using ReCodeItLib.Utils;
using System.Diagnostics;
using System.Reflection;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace ReCodeItLib.ReMapper;

public class ReMapper
{
    private ModuleDefMD? Module { get; set; }
    
    private static readonly Stopwatch Stopwatch = new();
    private string OutPath { get; set; } = string.Empty;
    
    private List<RemapModel> _remaps = [];

    private readonly List<string> _alreadyGivenNames = [];

    /// <summary>
    /// Start the remapping process
    /// </summary>
    public void InitializeRemap(
        List<RemapModel> remapModels,
        string targetAssemblyPath,
        string oldAssemblyPath,
        string outPath = "",
        bool validate = false)
    {
        _remaps = remapModels;
        
        targetAssemblyPath = AssemblyUtils.TryDeObfuscate(
            DataProvider.LoadModule(targetAssemblyPath), 
            targetAssemblyPath, 
            out var module);

        Module = module;
        
        OutPath = outPath;

        if (!Validate(_remaps)) return;
        
        Stopwatch.Start();
        
        var types = Module.GetTypes();

        var typeDefs = types as TypeDef[] ?? types.ToArray();
        if (!validate)
        {
            GenerateDynamicRemaps(targetAssemblyPath, typeDefs);
        }
        
        FindBestMatches(typeDefs, validate);
        ChooseBestMatches();

        // Don't go any further during a validation
        if (validate)
        {
            new Statistics(_remaps, Stopwatch, OutPath)
                .DisplayStatistics(true);
            
            return;
        }
        
        RenameMatches(typeDefs);
        Publicize();
        ApplyAttributeToRenamedClasses(oldAssemblyPath);
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
            while (!tasks.TrueForAll(t => t.Status is TaskStatus.RanToCompletion or TaskStatus.Faulted))
            {
                Logger.DrawProgressBar(tasks.Count(t => t.IsCompleted), tasks.Count, 50);
            }
        }
        
        Task.WaitAll(tasks.ToArray());
    }

    private void RenameMatches(IEnumerable<TypeDef> types)
    {
        Logger.LogSync("\nRenaming...", ConsoleColor.Green);

        var renamer = new Renamer();
        
        var renameTasks = new List<Task>(_remaps.Count);
        foreach (var remap in _remaps)
        {
            renameTasks.Add(
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        renamer.RenameAll(types, remap);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogSync($"Exception in task: {ex.Message}", ConsoleColor.Red);
                    }
                })
            );
        }
        
        while (!renameTasks.TrueForAll(t => t.Status is TaskStatus.RanToCompletion or TaskStatus.Faulted))
        {
            Logger.DrawProgressBar(renameTasks.Count(t => t.IsCompleted), renameTasks.Count, 50);
        }
        
        Task.WaitAll(renameTasks.ToArray());
    }

    private void Publicize()
    {
        Logger.LogSync("\nPublicizing classes...", ConsoleColor.Green);
        
        var publicizer = new Publicizer();
        
        var publicizeTasks = new List<Task>(Module!.Types.Count(t => !t.IsNested));
        foreach (var type in Module!.Types)
        {
            if (type.IsNested) continue; // Nested types are handled when publicizing the parent type
            
            publicizeTasks.Add(
                Task.Run(() =>
                {
                    try
                    {
                        publicizer.PublicizeType(type);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogSync($"Exception in task: {ex.Message}", ConsoleColor.Red);
                    }
                })
            );
        }

        Task.WaitAll(publicizeTasks.ToArray());
    }
    
    private static bool Validate(List<RemapModel> remaps)
    {
        var duplicateGroups = remaps
            .GroupBy(m => m.NewTypeName)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicateGroups.Count <= 1) return true;
        
        Logger.Log($"There were {duplicateGroups.Count} duplicated sets of remaps.", ConsoleColor.Yellow);

        foreach (var duplicate in duplicateGroups)
        {
            var duplicateNewTypeName = duplicate.Key;
            Logger.Log($"Ambiguous NewTypeName: {duplicateNewTypeName} found. Cancelling Remap.", ConsoleColor.Red);
        }

        return false;
    }

    /// <summary>
    /// First we filter our type collection based on simple search parameters (true/false/null)
    /// where null is a third disabled state. Then we score the types based on the search parameters
    /// </summary>
    /// <param name="mapping">Mapping to score</param>
    /// <param name="types">Types to filter</param>
    private void ScoreMapping(RemapModel mapping, IEnumerable<TypeDef> types)
    {
        var tokens = DataProvider.Settings.TypeNamesToMatch;

        if (mapping.UseForceRename)
        {
            HandleDirectRename(mapping, ref types);
            return;
        }

        // Filter down nested objects
        types = !mapping.SearchParams.NestedTypes.IsNested
            ? types.Where(type => tokens.Any(token => type.Name.StartsWith(token)))
            : types.Where(t => t.DeclaringType != null);

        if (mapping.SearchParams.NestedTypes.NestedTypeParentName != string.Empty)
        {
            types = types.Where(t => t.DeclaringType.Name == mapping.SearchParams.NestedTypes.NestedTypeParentName);
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
            if (type.Name != mapping.OriginalTypeName) continue;
            
            mapping.TypePrimeCandidate = type;
            mapping.OriginalTypeName = type.Name.String;
            mapping.Succeeded = true;

            _alreadyGivenNames.Add(mapping.OriginalTypeName);

            return;
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

    private void ApplyAttributeToRenamedClasses(string oldAssemblyPath)
    {
        // Create the attribute
        var annotationType = new TypeDefUser(
            "SPT", 
            "SPTRenamedClassAttribute",
            Module!.CorLibTypes.Object.TypeDefOrRef)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.AutoLayout | 
                         TypeAttributes.Class | TypeAttributes.AnsiClass,
            
            BaseType = Module.Import(typeof(Attribute)),
        };
        
        // Add fields
        annotationType.Fields.Add(new FieldDefUser(
            "RenamedFrom", 
            new FieldSig(Module.CorLibTypes.String),
            FieldAttributes.Public | FieldAttributes.InitOnly));
        
        annotationType.Fields.Add(new FieldDefUser(
            "HasChangesFromPreviousVersion", 
            new FieldSig(Module.CorLibTypes.Boolean),
            FieldAttributes.Public | FieldAttributes.InitOnly));
        
        // Create the constructor
        var ctor = new MethodDefUser(".ctor", MethodSig.CreateInstance(Module.CorLibTypes.Void, Module.CorLibTypes.String, Module.CorLibTypes.Boolean),
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            MethodAttributes.Public |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
        
        // Add the ctor method
        annotationType.Methods.Add(ctor);
        
        // Name the ctor parameters
        ctor.Parameters[1].CreateParamDef();
        ctor.Parameters[1].ParamDef.Name = "renamedFrom";
        ctor.Parameters[2].CreateParamDef();
        ctor.Parameters[2].ParamDef.Name = "hasChangesFromPreviousVersion";
        
        // Create the body
        ctor.Body = new CilBody();
        
        ctor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
        ctor.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
        ctor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(annotationType.Fields[0]));

        ctor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
        ctor.Body.Instructions.Add(OpCodes.Ldarg_2.ToInstruction());
        ctor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(annotationType.Fields[1])); 
        ctor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        
        // Add the attribute to the assembly
        Module.Types.Add(annotationType);
        
        var attributeCtor = annotationType.FindMethod(".ctor");
        
        var diff = new DiffCompare(DataProvider.LoadModule(oldAssemblyPath));
        
        foreach (var type in _remaps)
        {
            var customAttribute = new CustomAttribute(Module.Import(attributeCtor));
            customAttribute.ConstructorArguments.Add(new CAArgument(Module.CorLibTypes.String, type.OriginalTypeName));
            customAttribute.ConstructorArguments.Add(new CAArgument(Module.CorLibTypes.Boolean, diff.IsSame(type.TypePrimeCandidate!)));
            type.TypePrimeCandidate!.CustomAttributes.Add(customAttribute);
        }
    }
    
    /// <summary>
    /// Write the assembly back to disk and update the mapping file on disk
    /// </summary>
    private void WriteAssembly()
    {
        var moduleName = Module?.Name;

        const string dllName = "-cleaned-remapped-publicized.dll";
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
        
        StartHollow();

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
        
        if (DataProvider.Settings.MappingPath != string.Empty)
        {
            DataProvider.UpdateMapping(DataProvider.Settings.MappingPath.Replace("mappings.", "mappings-new."), _remaps);
        }

        new Statistics(_remaps, Stopwatch, OutPath, hollowedPath)
            .DisplayStatistics();
        
        Stopwatch.Reset();
        Module = null;
    }

    /// <summary>
    /// Hollows out all logic from the dll
    /// </summary>
    private void StartHollow()
    {
        Logger.LogSync("Creating Hollow...", ConsoleColor.Green);
        
        var tasks = new List<Task>(Module!.GetTypes().Count());
        foreach (var type in Module.GetTypes())
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    HollowType(type);
                }
                catch (Exception ex)
                {
                    Logger.LogSync($"Exception in task: {ex.Message}", ConsoleColor.Red);
                }
            }));
        }
        
        Task.WaitAll(tasks.ToArray());
    }

    private void HollowType(TypeDef type)
    {
        foreach (var method in type.Methods.Where(m => m.HasBody))
        {
            if (!method.HasBody) continue;

            method.Body = new CilBody();
            method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        }
    }
}