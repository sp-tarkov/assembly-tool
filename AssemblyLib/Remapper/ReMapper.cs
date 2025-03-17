using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Diagnostics;
using System.Reflection;
using AssemblyLib.Application;
using AssemblyLib.Enums;
using AssemblyLib.Models;
using AssemblyLib.Utils;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace AssemblyLib.ReMapper;

public class ReMapper
{
    private ModuleDefMD? Module { get; set; }
    private string OutPath { get; set; } = string.Empty;
    
    private readonly List<string> _alreadyGivenNames = [];
    
    /// <summary>
    /// Start the remapping process
    /// </summary>
    public void InitializeRemap(
        string targetAssemblyPath,
        string oldAssemblyPath,
        string outPath = "",
        bool validate = false)
    {
        Logger.Stopwatch.Start();
        
        targetAssemblyPath = AssemblyUtils.TryDeObfuscate(
            DataProvider.LoadModule(targetAssemblyPath), 
            targetAssemblyPath, 
            out var module);

        Module = module;
        
        OutPath = outPath;

        if (!Validate(DataProvider.Remaps)) return;
        
        var types = Module.GetTypes();

        var typeDefs = types as TypeDef[] ?? types.ToArray();
        
        InitializeComponents(typeDefs, oldAssemblyPath);
        if (!validate)
        {
            GenerateDynamicRemaps(targetAssemblyPath, typeDefs);
        }
        
        FindBestMatches(typeDefs, validate);
        ChooseBestMatches();

        // Don't go any further during a validation
        if (validate)
        {
            Context.Instance.Get<Statistics>()
                .DisplayStatistics(true);
            
            return;
        }
        
        RenameMatches(typeDefs);
        Publicize();
        
        if (!string.IsNullOrEmpty(oldAssemblyPath))
        {
            CreateCustomTypeAttribute();
        }
        
        WriteAssembly();
    }

    private void InitializeComponents(TypeDef[] types, string oldAssemblyPath)
    {
        var ctx = Context.Instance;

        var stats = new Statistics();
        var renamer = new Renamer(types, stats);
        var publicizer = new Publicizer(stats);
        
        ctx.RegisterComponent<Statistics>(stats);
        ctx.RegisterComponent<Renamer>(renamer);
        ctx.RegisterComponent<Publicizer>(publicizer);
        
        if (!string.IsNullOrEmpty(oldAssemblyPath))
        {
            var diff = new DiffCompare(DataProvider.LoadModule(oldAssemblyPath));
            ctx.RegisterComponent<DiffCompare>(diff);
        }
    }
    
    private void FindBestMatches(IEnumerable<TypeDef> types, bool validate)
    {
        var tasks = new List<Task>(DataProvider.Remaps.Count);
        foreach (var remap in DataProvider.Remaps)
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
            Logger.Log("Finding Best Matches...", ConsoleColor.Green);
            while (!tasks.TrueForAll(t => t.Status is TaskStatus.RanToCompletion or TaskStatus.Faulted))
            {
                Logger.DrawProgressBar(tasks.Count(t => t.IsCompleted), tasks.Count, 50);
            }
        }
        
        Task.WaitAll(tasks.ToArray());
    }

    private void RenameMatches(IEnumerable<TypeDef> types)
    {
        var renameTasks = new List<Task>(DataProvider.Remaps.Count);
        foreach (var remap in DataProvider.Remaps)
        {
            renameTasks.Add(
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Context.Instance.Get<Renamer>()
                            !.RenameRemap(remap);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Exception in task: {ex.Message}", ConsoleColor.Red);
                    }
                })
            );
        }
        
        Logger.Log("\nRenaming Types and Members...", ConsoleColor.Green);
        while (!renameTasks.TrueForAll(t => t.Status is TaskStatus.RanToCompletion or TaskStatus.Faulted))
        {
            Logger.DrawProgressBar(renameTasks.Count(t => t.IsCompleted), renameTasks.Count, 50);
        }
        
        Task.WaitAll(renameTasks.ToArray());
    }

    private void Publicize()
    {
        var types = Module!.GetTypes();

        var typeDefs = types as TypeDef[] ?? types.ToArray();
        var publicizeTasks = new List<Task>(typeDefs.Length);
        
        foreach (var type in typeDefs)
        {
            publicizeTasks.Add(
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Context.Instance.Get<Publicizer>()
                            .PublicizeType(type);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Exception in task: {ex.Message}", ConsoleColor.Red);
                    }
                })
            );
        }
        
        Logger.Log("\nPublicizing Types...", ConsoleColor.Green);
        while (!publicizeTasks.TrueForAll(t => t.Status is TaskStatus.RanToCompletion or TaskStatus.Faulted))
        {
            Logger.DrawProgressBar(publicizeTasks.Count(t => t.IsCompleted), publicizeTasks.Count, 50);
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
            
            DataProvider.Remaps.Add(remap);
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

    private void CreateCustomTypeAttribute()
    {
        Logger.Log("\nCreating custom attribute...", ConsoleColor.Green);
        
        var corlibRef = new AssemblyRefUser(Module!.GetCorlibAssembly());
        
        // Create the attribute
        var annotationType = new TypeDefUser(
            "SPT", 
            "SPTRenamedClassAttribute",
            Module!.CorLibTypes.Object.TypeDefOrRef)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.AutoLayout | 
                         TypeAttributes.Class | TypeAttributes.AnsiClass,
            
            BaseType = new TypeRefUser(Module, "System", "Attribute", corlibRef),
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

        var attrTasks = new List<Task>(DataProvider.Remaps.Count);

        var diff = Context.Instance.Get<DiffCompare>();
        
        foreach (var mapping in DataProvider.Remaps)
        {
            attrTasks.Add(
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        AddAttrToType(mapping, attributeCtor, diff);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Exception in task: {ex.Message}", ConsoleColor.Red);
                    }
                })
            );
        }
        
        Logger.Log("Applying Custom attribute to types...", ConsoleColor.Green);
        while (!attrTasks.TrueForAll(t => t.Status is TaskStatus.RanToCompletion or TaskStatus.Faulted))
        {
            Logger.DrawProgressBar(attrTasks.Count(t => t.IsCompleted), attrTasks.Count, 50);
        }
        
        Task.WaitAll(attrTasks.ToArray());
    }

    private void AddAttrToType(RemapModel remap, MethodDef attrCtor, DiffCompare? diff) 
    {
        var customAttribute = new CustomAttribute(Module!.Import(attrCtor));
        customAttribute.ConstructorArguments.Add(new CAArgument(Module.CorLibTypes.String, remap.OriginalTypeName));

        if (diff is not null)
        {
            customAttribute.ConstructorArguments.Add(new CAArgument(Module.CorLibTypes.Boolean, diff.IsSame(remap.TypePrimeCandidate!)));
        }
            
        remap.TypePrimeCandidate!.CustomAttributes.Add(customAttribute);
    }
    
    /// <summary>
    /// Write the assembly back to disk and update the mapping file on disk
    /// </summary>
    private void WriteAssembly()
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
        
        StartHollow();

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
    private void StartHollow()
    {
        var tasks = new List<Task>(Module!.GetTypes().Count());
        foreach (var type in Module.GetTypes())
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
                    Logger.Log($"Exception in task: {ex.Message}", ConsoleColor.Red);
                }
            }));
        }
        
        Logger.Log("\nHollowing Types...", ConsoleColor.Green);
        while (!tasks.TrueForAll(t => t.Status is TaskStatus.RanToCompletion or TaskStatus.Faulted))
        {
            Logger.DrawProgressBar(tasks.Count(t => t.IsCompleted), tasks.Count, 50);
        }
        
        Task.WaitAll(tasks.ToArray());
    }

    private static void HollowType(TypeDef type)
    {
        foreach (var method in type.Methods.Where(m => m.HasBody))
        {
            method.Body = new CilBody();
            method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        }
    }
}