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

        LoadDirectMappingFile();
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

    public Dictionary<string, DirectMapModel> DirectMapModels { get; } = [];

    private static readonly string _assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
    private static readonly string _directMappingPath = Path.Combine(_assetsPath, "Json", "Mappings");

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

    private void LoadDirectMappingFile()
    {
        if (!Directory.Exists(_directMappingPath))
        {
            Log.Information("Cannot find mappings at: {Path}", _directMappingPath);
            return;
        }

        JsonSerializerOptions settings = new()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        var count = 0;
        foreach (var file in Directory.GetFiles(_directMappingPath))
        {
            var jsonText = File.ReadAllText(file);
            var tmp = JsonSerializer.Deserialize<Dictionary<string, DirectMapModel>>(jsonText, settings)!;

            foreach (var (name, model) in tmp)
            {
                if (!DirectMapModels.TryAdd(name, model))
                {
                    Log.Error("Duplicate DirectMapModel: {Name} found.", name);
                }

                count++;
            }

            Log.Information("Direct Mapping file loaded: {Path}", file);
        }

        Log.Information("Direct Mapping count: {Count}", count);
    }

    private static Settings LoadAppSettings()
    {
        var settingsPath = Path.Combine(_assetsPath, "Json", "Settings.jsonc");
        var jsonText = File.ReadAllText(settingsPath);

        JsonSerializerOptions settings = new() { AllowTrailingCommas = true };

        return JsonSerializer.Deserialize<Settings>(jsonText, settings)!;
    }
}
