using System.Text.Json;
using System.Text.Json.Serialization;
using AsmResolver.DotNet;
using AssemblyLib.Models;
using Serilog;
using SPTarkov.DI.Annotations;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace AssemblyLib.Shared;

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
    public ModuleDefinition? LoadedModule { get; private set; }
    public ModuleDefinition? Mscorlib { get; private set; }

    public bool IsModuleLoaded
    {
        get { return LoadedModule != null; }
    }

    public bool IsMscorlibLoaded
    {
        get { return Mscorlib != null; }
    }

    public Dictionary<string, ItemTemplateModel> ItemTemplates { get; private set; }

    private readonly List<RemapModel> _remaps = [];
    private readonly Lock _remapLock = new();
    private static readonly string _assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
    private static readonly string _mappingPath = Path.Combine(_assetsPath, "Json", "mappings.jsonc");

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public ModuleDefinition LoadModule(string path, bool loadMscorlib = true)
    {
        var directory = Path.GetDirectoryName(path)!;

        var module = ModuleDefinition.FromFile(path);

        if (loadMscorlib)
        {
            Mscorlib = ModuleDefinition.FromFile(Path.Combine(directory, "MsCorLib.dll"));
        }

        LoadedModule = module ?? throw new NullReferenceException("Module is null...");
        return module;
    }

    public List<RemapModel> GetRemaps()
    {
        return _remaps;
    }

    public int RemapCount()
    {
        return _remaps.Count;
    }

    public void AddMapping(RemapModel remap)
    {
        lock (_remapLock)
        {
            _remaps.Add(remap);
        }
    }

    public bool RemoveMapping(RemapModel remap)
    {
        lock (_remapLock)
        {
            return _remaps.Remove(remap);
        }
    }

    public void ClearMappings()
    {
        lock (_remapLock)
        {
            _remaps.Clear();
        }
    }

    public string SerializeRemap(RemapModel remap)
    {
        return JsonSerializer.Serialize(remap, _serializerOptions);
    }

    public void UpdateMappingFile(bool respectNullableAnnotations = true)
    {
        JsonSerializerOptions settings = new(_serializerOptions)
        {
            RespectNullableAnnotations = !respectNullableAnnotations,
        };

        // Clear out the dynamically generated remaps before writing
        foreach (var remap in _remaps.ToList().Where(remap => remap.UseForceRename))
        {
            _remaps.Remove(remap);
        }

        var jsonText = JsonSerializer.Serialize(_remaps, settings);

        File.WriteAllText(_mappingPath, jsonText);

        Log.Information("Mapping file updated with new type names and saved to {Path}", _mappingPath);
    }

    public void LoadMappingFile()
    {
        if (!File.Exists(_mappingPath))
        {
            Log.Information("Cannot find mapping.json at: {Path}", _mappingPath);
            return;
        }

        var jsonText = File.ReadAllText(_mappingPath);

        JsonSerializerOptions settings = new() { AllowTrailingCommas = true };

        var remaps = JsonSerializer.Deserialize<List<RemapModel>>(jsonText, settings);
        _remaps.AddRange(remaps!);

        ValidateMappings();
    }

    private void ValidateMappings()
    {
        var duplicateGroups = _remaps.GroupBy(m => m.NewTypeName).Where(g => g.Count() > 1).ToList();

        if (duplicateGroups.Count <= 1)
        {
            return;
        }

        foreach (var duplicate in duplicateGroups)
        {
            var duplicateNewTypeName = duplicate.Key;
            Log.Error("Ambiguous NewTypeName: {DuplicateNewTypeName} found. Cancelling Remap.", duplicateNewTypeName);
        }

        throw new Exception($"There are {duplicateGroups.Count} sets of duplicated remaps.");
    }

    private static Settings LoadAppSettings()
    {
        var settingsPath = Path.Combine(_assetsPath, "Json", "Settings.jsonc");
        var jsonText = File.ReadAllText(settingsPath);

        JsonSerializerOptions settings = new() { AllowTrailingCommas = true };

        return JsonSerializer.Deserialize<Settings>(jsonText, settings)!;
    }

    private Dictionary<string, ItemTemplateModel> LoadItems()
    {
        var itemsPath = Path.Combine(_assetsPath, "Json", "items.json");
        var jsonText = File.ReadAllText(itemsPath);

        JsonSerializerOptions settings = new()
        {
            RespectNullableAnnotations = true,
            PropertyNameCaseInsensitive = true,
        };

        return JsonSerializer.Deserialize<Dictionary<string, ItemTemplateModel>>(jsonText, settings)!;
    }
}
