using AsmResolver.DotNet;
using AssemblyLib.Shared;
using Serilog;
using Serilog.Events;
using Spectre.Console;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable(InjectionType.Singleton)]
public class DirectMapController(
    AssemblyWriter assemblyWriter,
    DataProvider dataProvider,
    RenamerService renamerService,
    Publicizer publicizer
)
{
    private ModuleDefinition? Module { get; set; }
    private List<TypeDefinition> Types { get; set; } = [];

    private string _targetAssemblyPath = string.Empty;

    public async Task Run(string assemblyPath)
    {
        Module = dataProvider.LoadModule(assemblyPath);
        _targetAssemblyPath = assemblyPath;

        if (!TryDeobfuscateAssembly())
        {
            return;
        }

        RunRenamingProcess();
        PublicizeObfuscatedTypes();
        await assemblyWriter.WriteAssembly(Module, _targetAssemblyPath);

        Log.Information("Direct map completed.");
    }

    private bool TryDeobfuscateAssembly()
    {
        var result = assemblyWriter.Deobfuscate(Module, _targetAssemblyPath);
        if (!result.Success)
        {
            return false;
        }

        _targetAssemblyPath =
            result.DeObfuscatedAssemblyPath ?? throw new NullReferenceException("Deobfuscated assembly path is null");
        Module = result.DeObfuscatedModule ?? throw new NullReferenceException("Deobfuscated module is null");

        Types.AddRange(Module?.GetAllTypes() ?? []);

        if (Types.Count == 0)
        {
            throw new InvalidOperationException("No types found during loading/deobfuscation of assembly");
        }

        return true;
    }

    private void RunRenamingProcess()
    {
        var mappings = dataProvider.DirectMapModels;

        if (mappings.Count == 0)
        {
            Log.Error("No direct-mappings loaded.");
            return;
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            foreach (var (targetFullName, mapping) in mappings)
            {
                renamerService.RenameMappingRecursive(targetFullName, mapping);
            }

            return;
        }

        AnsiConsole
            .Progress()
            .AutoClear(true)
            .StartAsync(ctx =>
            {
                var task = ctx.AddTask("[green]Renaming[/]", maxValue: mappings.Count);

                foreach (var (targetFullName, mapping) in mappings)
                {
                    renamerService.RenameMappingRecursive(targetFullName, mapping);
                    task.Increment(1.0);
                }

                return Task.CompletedTask;
            });

        // Make sure we don't do this until after renaming remaps
        renamerService.RenameCompilerGeneratedTypes();
    }

    private void PublicizeObfuscatedTypes()
    {
        AnsiConsole
            .Progress()
            .AutoClear(true)
            .StartAsync(ctx =>
            {
                var task = ctx.AddTask("[green]Publicizing[/]".PadLeft(25), maxValue: Types.Count);

                foreach (var type in Types)
                {
                    publicizer.PublicizeType(type);
                    task.Increment(1.0);
                }

                return Task.CompletedTask;
            });
    }
}
