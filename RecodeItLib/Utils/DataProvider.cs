using dnlib.DotNet;
using Newtonsoft.Json;
using ReCodeItLib.Models;
using ReCodeItLib.Dumper;

namespace ReCodeItLib.Utils;

public static class DataProvider
{
    static DataProvider()
    {
        LoadItems();
    }
    
    public static string DataPath => Path.Combine(AppContext.BaseDirectory, "Data");

    public static List<RemapModel> Remaps { get; set; } = [];
    public static Dictionary<string, ItemTemplateModel>? ItemTemplates { get; private set; }
    
    public static Settings? Settings { get; private set; }

    public static void LoadAppSettings()
    {
        var settingsPath = Path.Combine(DataPath, "Settings.jsonc");

        var jsonText = File.ReadAllText(settingsPath);

        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        Settings = JsonConvert.DeserializeObject<Settings>(jsonText, settings);
    }
    
    public static List<RemapModel> LoadMappingFile(string path)
    {
        if (!File.Exists(path))
        {
            Logger.Log($"Error loading mapping.json from `{path}`, First time running? Please select a mapping path in the gui", ConsoleColor.Red);
            return [];
        }

        var jsonText = File.ReadAllText(path);

        var remaps = JsonConvert.DeserializeObject<List<RemapModel>>(jsonText);
        
        return remaps ?? [];
    }
    
    public static void UpdateMapping(string path, List<RemapModel> remaps)
    {
        if (!File.Exists(path))
        {
            File.Create(path).Close();
        }

        JsonSerializerSettings settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        var jsonText = JsonConvert.SerializeObject(remaps, settings);

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

    private static void LoadItems()
    {
        var itemsPath = Path.Combine(DataPath, "items.json");
        var jsonText = File.ReadAllText(itemsPath);

        ItemTemplates = JsonConvert.DeserializeObject<Dictionary<string, ItemTemplateModel>>(jsonText);
    }
}