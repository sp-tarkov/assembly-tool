using System.Collections.Immutable;
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
    public RuntimeContext Context { get; private set;  }
    public ModuleDefinition? LoadedModule { get; private set; }
    public ModuleDefinition? DummyDllModule { get; private set; }
    public ModuleDefinition? Mscorlib { get; private set; }

    public bool IsDummyDllLoaded
    {
        get { return DummyDllModule != null; }
    }

    public Dictionary<string, DirectMapModel> DirectMapModels { get; } = [];

    private static readonly string _assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
    private static readonly string _directMappingPath = Path.Combine(_assetsPath, "Json", "Mappings");

    public static readonly ImmutableHashSet<string> ObfuscatedNames =
    [
        "Class",
        "Delegate",
        "GAttribute",
        "GException",
        "GDelegate",
        "Exception",
        "GClass",
        "GControl",
        "Struct",
        "GStruct",
        "Interface",
        "GInterface",
    ];

    public ModuleDefinition LoadModule(string path, bool loadMscorlib = true)
    {
        var directory = Path.GetDirectoryName(path)!;

        var module = ModuleDefinition.FromFile(path);

        if (loadMscorlib)
        {
            Mscorlib = ModuleDefinition.FromFile(Path.Combine(directory, "mscorlib.dll"));
        }

        LoadedModule = module ?? throw new NullReferenceException("Module is null...");
        Context = module.RuntimeContext ?? throw new NullReferenceException("Could not get runtime context!");
        return module;
    }

    public ModuleDefinition LoadDummyDllModule(string path)
    {
        var module = ModuleDefinition.FromFile(path);

        DummyDllModule = module ?? throw new NullReferenceException("Dummy Module is null...");
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

            count += CountMappingsRecursively(tmp);
            var localCount = 0;
            foreach (var (name, model) in tmp)
            {
                if (!DirectMapModels.TryAdd(name, model))
                {
                    Log.Error("Duplicate Found, {name}:{value}", name,  model.NewName);
                    continue;
                }

                localCount++;
            }

            Log.Information(
                "Direct Mapping file loaded {Count} mappings from: {Path}",
                localCount,
                Path.GetFileName(file)
            );
        }

        Log.Information("Total Count: {Count}", count);
    }

    private int CountMappingsRecursively(Dictionary<string, DirectMapModel> models)
    {
        // Don't count things we aren't renaming
        var count = models.Count(kvp => kvp.Value.NewName is not null);

        foreach (var (name, mapping) in models)
        {
            if (mapping.NestedTypes?.Count > 0)
            {
                count += CountMappingsRecursively(mapping.NestedTypes);
            }

            var dupe = DirectMapModels.Where(x => x.Value.NewName == mapping.NewName);
            if (dupe.Any())
            {
                // Only Log and deal with, this is a bad mapping issue
                Log.Error("Duplicate Found, {name}:{value}, Dupe: {name2}:{value2}", name,  mapping.NewName, dupe.First().Key, dupe.First().Value.NewName);
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
