using System.Text.Json;
using System.Text.Json.Serialization;
using AsmResolver.DotNet;
using AssemblyLib.Models;
using Serilog;
using SPTarkov.DI.Annotations;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace AssemblyLib.Utils;

[Injectable(InjectionType.Singleton)]
public class DataProvider
{
    public DataProvider()
    {
        Settings = LoadAppSettings();
        ItemTemplates = LoadItems();

        LoadMappingFile();
    }

    public Settings Settings { get; }

    public static List<RemapModel> Remaps { get; } = [];
    public static Dictionary<string, ItemTemplateModel> ItemTemplates { get; private set; }

    private static readonly string DataPath = Path.Combine(AppContext.BaseDirectory, "Data");
    private static readonly string MappingPath = Path.Combine(DataPath, "mappings.jsonc");
    private static readonly string MappingNewPath = Path.Combine(DataPath, "mappings-new.jsonc");

    public static ModuleDefinition Mscorlib { get; private set; }

    public ModuleDefinition LoadModule(string path, bool loadMscorlib = true)
    {
        var directory = Path.GetDirectoryName(path)!;

        var module = ModuleDefinition.FromFile(path);

        if (loadMscorlib)
        {
            Mscorlib = ModuleDefinition.FromFile(Path.Combine(directory, "MsCorLib.dll"));
        }

        if (module is null)
        {
            throw new NullReferenceException("Module is null...");
        }

        return module;
    }

    public void UpdateMapping(bool respectNullableAnnotations = true, bool isAutoMatch = false)
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

        Log.Information("Mapping file updated with new type names and saved to {Path}", path);
    }

    public void LoadMappingFile()
    {
        if (!File.Exists(MappingPath))
        {
            Log.Information("Cannot find mapping.json at: {Path}", MappingPath);
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

    private void ValidateMappings()
    {
        var duplicateGroups = Remaps
            .GroupBy(m => m.NewTypeName)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicateGroups.Count <= 1) return;

        foreach (var duplicate in duplicateGroups)
        {
            var duplicateNewTypeName = duplicate.Key;
            Log.Error("Ambiguous NewTypeName: {DuplicateNewTypeName} found. Cancelling Remap.", duplicateNewTypeName);
        }

        throw new Exception($"There are {duplicateGroups.Count} sets of duplicated remaps.");
    }

    private Settings LoadAppSettings()
    {
        var settingsPath = Path.Combine(DataPath, "Settings.jsonc");
        var jsonText = File.ReadAllText(settingsPath);

        JsonSerializerOptions settings = new()
        {
            AllowTrailingCommas = true,
        };

        return JsonSerializer.Deserialize<Settings>(jsonText, settings)!;
    }

    private Dictionary<string, ItemTemplateModel> LoadItems()
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