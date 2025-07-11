using AssemblyLib.ReMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SPTarkov.DI;

namespace AssemblyLib;

public class App
{
    private ServiceProvider? _provider;
    
    public App()
    {
        ConfigureLogger();
        ConfigureApplication();
    }

    public async Task RunRemapProcess(
        string assemblyPath, 
        string? oldAssemblyPath, 
        string outPath,
        bool validate = false
        )
    {
        var controller = _provider?.GetService<MappingController>();

        await controller?.Run(assemblyPath, oldAssemblyPath, outPath, validate)!;
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
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(config).CreateLogger();
    }
}