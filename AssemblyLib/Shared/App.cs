using AssemblyLib.DirectMapper;
using AssemblyLib.Remapper;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SPTarkov.DI;

namespace AssemblyLib.Shared;

public class App
{
    private ServiceProvider? _provider;

    public App()
    {
        ConfigureLogger();
        ConfigureApplication();
    }

    public Task RunRemapProcess(string assemblyPath, string? oldAssemblyPath, string outPath, bool validate = false)
    {
        var controller = _provider?.GetService<MappingController>();
        controller?.Run(assemblyPath, oldAssemblyPath, outPath, validate);

        return Task.CompletedTask;
    }

    public async Task RunDirectMapProcess(string targetAssemblyPath)
    {
        var controller = _provider?.GetService<DirectMapController>();
        await controller?.Run(targetAssemblyPath)!;
    }

    public async Task RunAutoMatcher(
        string assemblyPath,
        string oldAssemblyPath,
        string oldTypeName,
        string newTypeName,
        bool isRegen
    )
    {
        var controller = _provider?.GetService<AutoMatcher.AutoMatchController>();
        await controller?.AutoMatch(assemblyPath, oldAssemblyPath, oldTypeName, newTypeName, isRegen)!;
    }

    public Task DeObfuscate(string assemblyPath, bool isLauncher)
    {
        var controller = _provider?.GetService<AssemblyWriter>();

        Log.Information("Deobfuscating assembly...");

        controller?.Deobfuscate(assemblyPath, isLauncher);

        Log.Information("Complete...");

        return Task.CompletedTask;
    }

    public Task RunStatistics(string assemblyPath)
    {
        var statistics = _provider?.GetService<Statistics>();
        statistics?.DisplayAssemblyStatistics(assemblyPath);

        return Task.CompletedTask;
    }

    private void ConfigureApplication()
    {
        var services = new ServiceCollection();
        var diHandler = new DependencyInjectionHandler(services);

        diHandler.AddInjectableTypesFromTypeAssembly(typeof(App));
        diHandler.InjectAll();

        _provider = services.BuildServiceProvider();
    }

    private static void ConfigureLogger()
    {
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                "logs/bot-.log",
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10_000_000,
                retainedFileCountLimit: 50
            )
            .CreateLogger();
    }
}
