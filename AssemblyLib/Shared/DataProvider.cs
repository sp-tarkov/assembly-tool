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

            var localCount = 0;
            foreach (var (name, model) in tmp)
            {
                if (!DirectMapModels.TryAdd(name, model))
                {
                    Log.Error("Duplicate DirectMapModel: {Name} found.", name);
                    continue;
                }

                localCount++;
            }

            count += CountMappingsRecursively(tmp);
            Log.Information(
                "Direct Mapping file loaded {Count} mappings from: {Path}",
                localCount,
                Path.GetFileName(file)
            );
        }

        Log.Information("Total Count: {Count}", count);
    }

    private static int CountMappingsRecursively(Dictionary<string, DirectMapModel> models)
    {
        // Don't count things we aren't renaming
        var count = models.Count(kvp => kvp.Value.NewName is not null);

        foreach (var (_, mapping) in models)
        {
            if (mapping.NestedTypes?.Count > 0)
            {
                count += CountMappingsRecursively(mapping.NestedTypes);
            }
        }

        return count;
    }

    private static Settings LoadAppSettings()
    {
        var settingsPath = Path.Combine(_assetsPath, "Json", "Settings.jsonc");
        var jsonText = File.ReadAllText(settingsPath);

        JsonSerializerOptions settings = new() { AllowTrailingCommas = true };

        return JsonSerializer.Deserialize<Settings>(jsonText, settings)!;
    }
}
