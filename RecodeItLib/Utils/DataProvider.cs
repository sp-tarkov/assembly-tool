using System.Text.Json;
using System.Text.Json.Serialization;
using dnlib.DotNet;
using Newtonsoft.Json;
using ReCodeItLib.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ReCodeItLib.Utils;

public static class DataProvider
{
    static DataProvider()
    {
        Settings = LoadAppSettings();
        ItemTemplates = LoadItems();
    }
    
    public static Settings Settings { get; }
    
    public static Dictionary<string, ItemTemplateModel> ItemTemplates { get; private set; }
    
    private static readonly string DataPath = Path.Combine(AppContext.BaseDirectory, "Data");
    
    public static List<RemapModel> LoadMappingFile(string path)
    {
        if (!File.Exists(path))
        {
            Logger.Log($"Cannot find mapping.json at `{path}`", ConsoleColor.Red);
            return [];
        }

        var jsonText = File.ReadAllText(path);
        
        JsonSerializerOptions settings = new()
        {
            AllowTrailingCommas = true,
        };
        
        var remaps = JsonSerializer.Deserialize<List<RemapModel>>(jsonText, settings);
        
        return remaps ?? [];
    }
    
    public static void UpdateMapping(string path, List<RemapModel> remaps, bool respectNullableAnnotations = true)
    {
        if (!File.Exists(path))
        {
            File.Create(path).Close();
        }

        JsonSerializerOptions settings = new()
        {
            WriteIndented = true,
            RespectNullableAnnotations = !respectNullableAnnotations,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var jsonText = JsonSerializer.Serialize(remaps, settings);

        File.WriteAllText(path, jsonText);

        Logger.Log($"Mapping file updated with new type names and saved to {path}", ConsoleColor.Green);
    }

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