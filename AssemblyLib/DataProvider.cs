using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using AsmResolver.DotNet;
using AssemblyLib.Models;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace AssemblyLib;

[Injectable(InjectionType.Singleton)]
public class DataProvider
{
    public DataProvider(ILogger<DataProvider> logger)
    {
        _logger = logger;
        Settings = LoadAppSettings();

        LoadDirectMappingFile();

        Instance = this;
    }

    public static DataProvider Instance { get; private set; } = null!;

    public Settings Settings { get; }
    public RuntimeContext Context { get; private set; } = null!;
    public ModuleDefinition? LoadedModule { get; private set; }
    public ModuleDefinition? DummyDllModule { get; private set; }
    public ModuleDefinition? StubModule { get; private set; }

    public bool IsDummyDllLoaded => DummyDllModule != null;

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
        "method",
        "smethod",
        "vmethod",
    ];

    private readonly ILogger<DataProvider> _logger;

    public ModuleDefinition LoadModule(string path)
    {
        var directory = Path.GetDirectoryName(path)!;

        var asm = AssemblyDefinition.FromFile(path);
        var module = asm.Modules.FirstOrDefault();

        LoadedModule = module ?? throw new NullReferenceException("Module is null...");
        Context = asm.RuntimeContext ?? throw new NullReferenceException("Could not get runtime context!");

        _logger.LogInformation("Loaded target module: {moduleName}", module.Name?.ToString() ?? "NULL");

        foreach (var dll in Directory.GetFiles(directory, "*.dll"))
        {
            if (dll.Contains("Assembly-CSharp"))
            {
                continue;
            }

            Context.LoadAssembly(dll);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Loaded dependent module: {dll}", Path.GetFileNameWithoutExtension(dll));
            }
        }

        var codeStub = Path.Combine(AppContext.BaseDirectory, "EftCodeStub.dll");
        if (File.Exists(codeStub))
        {
            StubModule =
                Context.LoadAssembly(codeStub).Modules.FirstOrDefault()
                ?? throw new NullReferenceException("Could not load assembly code stub!");

            _logger.LogInformation("Loaded code stub module: EftCodeStub.dll");
        }

        return module;
    }

    public void LoadDummyDllModule(string path)
    {
        var module = ModuleDefinition.FromFile(path);

        DummyDllModule = module ?? throw new NullReferenceException("Dummy Module is null...");
    }

    private void LoadDirectMappingFile()
    {
        if (!Directory.Exists(_directMappingPath))
        {
            _logger.LogInformation("Cannot find mappings at: {Path}", _directMappingPath);
            return;
        }

        JsonSerializerOptions settings = new()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

        var count = 0;
        foreach (var file in Directory.GetFiles(_directMappingPath))
        {
            var jsonText = File.ReadAllText(file);
            var tmp = JsonSerializer.Deserialize<Dictionary<string, DirectMapModel>>(jsonText, settings)!;

            count += CountMappingsRecursively(tmp, file);
            var localCount = 0;
            foreach (var (name, model) in tmp)
            {
                if (!DirectMapModels.TryAdd(name, model))
                {
                    throw new DuplicateDirectMapException($"Duplicate direct mapping found, {name}");
                }

                localCount++;
            }

            _logger.LogInformation(
                "Direct Mapping file loaded {Count} mappings from: {Path}",
                localCount,
                Path.GetFileName(file)
            );
        }

        ValidateDuplicateNewNames(DirectMapModels);
        _logger.LogInformation("Total Count: {Count}", count);
    }

    private static int CountMappingsRecursively(Dictionary<string, DirectMapModel> models, string file)
    {
        // Don't count things we aren't renaming
        var count = models.Count(kvp => kvp.Value.NewName is not null);

        foreach (var (_, mapping) in models)
        {
            if (mapping.NestedTypes?.Count > 0)
            {
                count += CountMappingsRecursively(mapping.NestedTypes, file);
            }
        }

        return count;
    }

    private static void ValidateDuplicateNewNames(
        Dictionary<string, DirectMapModel> models,
        string? parentName = null,
        HashSet<string>? seenNames = null
    )
    {
        seenNames ??= [];

        foreach (var (originalName, mapping) in models)
        {
            // Use original name as fallback so nested paths are always well-formed
            var effectiveName = mapping.NewName ?? originalName;

            var currentName =
                parentName != null ? $"{parentName}.{effectiveName}"
                : mapping.NewNamespace != null ? $"{mapping.NewNamespace}.{effectiveName}"
                : effectiveName;

            // Only validate entries that actually declare a new name
            if (mapping.NewName != null)
            {
                var arity = originalName.Contains('`') ? originalName.Split('`')[1] : null;
                var seenKey = arity != null ? $"{currentName}`{arity}" : currentName;

                if (!seenNames.Add(seenKey))
                {
                    throw new DuplicateDirectMapException($"Duplicate direct mapping new name found: {currentName}");
                }
            }

            if (mapping.NestedTypes?.Count > 0)
            {
                ValidateDuplicateNewNames(mapping.NestedTypes, currentName, seenNames);
            }
        }
    }

    private static Settings LoadAppSettings()
    {
        var settingsPath = Path.Combine(_assetsPath, "Json", "Settings.jsonc");
        var jsonText = File.ReadAllText(settingsPath);

        JsonSerializerOptions settings = new() { AllowTrailingCommas = true };

        return JsonSerializer.Deserialize<Settings>(jsonText, settings)!;
    }
}
