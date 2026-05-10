using System.Reflection;
using System.Runtime.Loader;
using AssemblyLib.Helpers;
using AssemblyLib.Models;
using AssemblyLib.Patching;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Extensions.Logging;
using SPTarkov.DI;

namespace AssemblyLib;

public class App
{
    private ServiceProvider? _provider;

    public App()
    {
        ConfigureApplication();
    }

    public void RunDirectMapProcess(string targetAssemblyPath, string? dummyDllPath)
    {
        var controller = _provider?.GetService<DirectMapController>();
        controller?.Run(targetAssemblyPath, dummyDllPath);
    }

    public void RunDirectMapTest(
        string targetAssemblyPath,
        string dummyDllPath,
        string targetType,
        DirectMapModel testModel
    )
    {
        Console.Clear();

        var dataProvider = _provider?.GetService<DataProvider>();
        dataProvider!.DirectMapModels.Clear();
        dataProvider.DirectMapModels.Add(targetType, testModel);

        var controller = _provider?.GetService<DirectMapController>();
        controller?.Run(targetAssemblyPath, dummyDllPath);
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

    public Task RunPatch(string assemblyPath)
    {
        var dataProvider = _provider?.GetService<DataProvider>();
        var patchService = _provider?.GetService<PatchService>();
        var assemblyRefHelper = _provider?.GetService<AssemblySelfReferenceHelper>();
        var assemblyWriter = _provider?.GetService<AssemblyWriter>();

        var module = dataProvider?.LoadModule(assemblyPath);
        if (module == null)
        {
            Log.Error("Module is null while trying to apply patches via command");
            return Task.CompletedTask;
        }

        patchService?.ApplyPatches();
        assemblyRefHelper?.RemoveSelfAssemblyReferences(module);
        assemblyWriter?.WriteAssembly(module, assemblyPath);

        return Task.CompletedTask;
    }

    private void ConfigureApplication()
    {
        RegisterAssemblyResolve();

        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Information()
#else
            .MinimumLevel.Information()
#endif
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                "logs/assembly-tool-.log",
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10_000_000,
                retainedFileCountLimit: 50
            )
            .CreateLogger();

        var services = new ServiceCollection();
        var diHandler = new DependencyInjectionHandler(services);

        services.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory(Log.Logger, dispose: true));
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        diHandler.AddInjectableTypesFromTypeAssembly(typeof(App));
        diHandler.InjectAll();

        _provider = services.BuildServiceProvider();
    }

    private static void RegisterAssemblyResolve()
    {
        AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
        {
            var baseDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Assemblies");

            if (assemblyName.Name != "0harmony")
            {
                baseDir = Path.Combine(baseDir, "AssemblyCSharp");
            }

            var candidate = Path.Combine(baseDir, assemblyName.Name + ".dll");

            if (File.Exists(candidate))
            {
                return context.LoadFromAssemblyPath(candidate);
            }

            return null;
        };

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var assemblyName = new AssemblyName(args.Name).Name;

            var baseDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Assemblies", "AssemblyCSharp");

            var candidate = Path.Combine(baseDir, assemblyName + ".dll");

            if (File.Exists(candidate))
            {
                return Assembly.LoadFrom(candidate);
            }

            return null;
        };
    }
}
