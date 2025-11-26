using AsmResolver.DotNet;
using AssemblyLib.DirectMapper.Patches;
using AssemblyLib.DirectMapper.Renamers;
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
    SigBasedMemberRenamer sigBasedMemberRenamer,
    Publicizer publicizer,
    MemberReferenceCache memberReferenceCache,
    IEnumerable<IPatch> patches
)
{
    private ModuleDefinition? Module { get; set; }
    private List<TypeDefinition> Types { get; set; } = [];

    private string _targetAssemblyPath = string.Empty;

    public async Task Run(string assemblyPath, string? dummyDllPath)
    {
        Module = dataProvider.LoadModule(assemblyPath);
        _targetAssemblyPath = assemblyPath;

        memberReferenceCache.Hydrate();

        if (!TryDeobfuscateAssembly())
        {
            return;
        }

        await RunRenamingProcess();

        if (!string.IsNullOrEmpty(dummyDllPath))
        {
            dataProvider.LoadDummyDllModule(dummyDllPath);
            Log.Information("Dummy DLL loaded.");
            await RenameBySignature();
        }

        await PublicizeObfuscatedTypes();
        await ApplyPatches();
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

    private async Task RunRenamingProcess()
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
                await renamerService.RenameMappingRecursive(targetFullName, mapping);
            }

            return;
        }

        await AnsiConsole
            .Progress()
            .AutoClear(true)
            .StartAsync(ctx =>
            {
                var ctxTask = ctx.AddTask("[green]Renaming[/]", maxValue: mappings.Count);

                var tasks = new List<Task>(mappings.Count);

                foreach (var (targetFullName, mapping) in mappings)
                {
                    var task = Task.Factory.StartNew(async () =>
                    {
                        await renamerService.RenameMappingRecursive(targetFullName, mapping);
                        ctxTask.Increment(1.0);
                    });

                    tasks.Add(task);
                }

                return Task.WhenAll(tasks);
            });

        // Make sure we don't do this until after renaming remaps
        renamerService.RenameCompilerGeneratedTypes();
    }

    private Task ApplyPatches()
    {
        foreach (var patch in patches)
        {
            patch.Patch();
        }

        return Task.CompletedTask;
    }

    private Task RenameBySignature()
    {
        sigBasedMemberRenamer.RenameMembersBySignature();
        return Task.CompletedTask;
    }

    private async Task PublicizeObfuscatedTypes()
    {
        await AnsiConsole
            .Progress()
            .AutoClear(true)
            .StartAsync(ctx =>
            {
                var ctxTask = ctx.AddTask("[green]Publicizing[/]".PadLeft(25), maxValue: Types.Count);
                var tasks = new List<Task>(Types.Count);

                foreach (var type in Types)
                {
                    var task = Task.Factory.StartNew(async () =>
                    {
                        await publicizer.PublicizeType(type);
                        ctxTask.Increment(1.0);
                    });

                    tasks.Add(task);
                }

                return Task.WhenAll(tasks);
            });
    }
}
