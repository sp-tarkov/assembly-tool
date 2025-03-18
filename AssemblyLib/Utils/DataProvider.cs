using System.Text.Json;
using System.Text.Json.Serialization;
using AssemblyLib.Models;
using dnlib.DotNet;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace AssemblyLib.Utils;

public static class DataProvider
{
    static DataProvider()
    {
        Settings = LoadAppSettings();
        ItemTemplates = LoadItems();
        
        LoadMappingFile();
    }
    
    public static Settings Settings { get; }
    
    public static List<RemapModel> Remaps { get; } = [];
    public static Dictionary<string, ItemTemplateModel> ItemTemplates { get; private set; }
    
    private static readonly string DataPath = Path.Combine(AppContext.BaseDirectory, "Data");
    private static readonly string MappingPath = Path.Combine(DataPath, "mappings.jsonc");
    private static readonly string MappingNewPath = Path.Combine(DataPath, "mappings-new.jsonc");
    
    public static ModuleDefMD LoadModule(string path)
    {
        var mcOptions = new ModuleCreationOptions(ModuleDef.CreateModuleContext());
        var module = ModuleDefMD.Load(path, mcOptions);

        module.Context = mcOptions.Context;

        if (module is null)
        {
            throw new NullReferenceException("Module is null...");
        }

        return module;
    }
    
    public static void UpdateMapping(bool respectNullableAnnotations = true, bool isAutoMatch = false)
    {
        if (!File.Exists(MappingNewPath))
        {
            File.Create(MappingNewPath).Close();
        }

        JsonSerializerOptions settings = new()
        {
            WriteIndented = true,
            RespectNullableAnnotations = !respectNullableAnnotations,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var jsonText = JsonSerializer.Serialize(Remaps, settings);

        var path = isAutoMatch
            ? MappingPath
            : MappingNewPath;
        
        File.WriteAllText(path, jsonText);

        Logger.Log($"Mapping file updated with new type names and saved to {path}", ConsoleColor.Green);
    }

    public static void LoadMappingFile()
    {
        if (!File.Exists(MappingPath))
        {
            Logger.Log($"Cannot find mapping.json at `{MappingPath}`", ConsoleColor.Red);
            return;
        }

        var jsonText = File.ReadAllText(MappingPath);
        
        JsonSerializerOptions settings = new()
        {
            AllowTrailingCommas = true,
        };
        
        var remaps = JsonSerializer.Deserialize<List<RemapModel>>(jsonText, settings);
        Remaps.AddRange(remaps!);

        ValidateMappings();
    }
    
    private static void ValidateMappings()
    {
        var duplicateGroups = Remaps
            .GroupBy(m => m.NewTypeName)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicateGroups.Count <= 1) return;
        
        foreach (var duplicate in duplicateGroups)
        {
            var duplicateNewTypeName = duplicate.Key;
            Logger.Log($"Ambiguous NewTypeName: {duplicateNewTypeName} found. Cancelling Remap.", ConsoleColor.Red);
        }
        
        throw new Exception($"There are {duplicateGroups.Count} sets of duplicated remaps.");
    }
    
    private static Settings LoadAppSettings()
    {
        var settingsPath = Path.Combine(DataPath, "Settings.jsonc");
        var jsonText = File.ReadAllText(settingsPath);
        
        JsonSerializerOptions settings = new()
        {
            AllowTrailingCommas = true,
        };
        
        return JsonSerializer.Deserialize<Settings>(jsonText, settings)!;
    }
    
    private static Dictionary<string, ItemTemplateModel> LoadItems()
    {
        var itemsPath = Path.Combine(DataPath, "items.json");
        var jsonText = File.ReadAllText(itemsPath);

        JsonSerializerOptions settings = new()
        {
            RespectNullableAnnotations = true,
            PropertyNameCaseInsensitive = true
        };
        
        return JsonSerializer.Deserialize<Dictionary<string, ItemTemplateModel>>(jsonText, settings)!;
    }
}