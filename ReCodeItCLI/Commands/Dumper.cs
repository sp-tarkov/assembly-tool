using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ReCodeIt.Utils;
using ReCodeItLib.Dumper;

namespace ReCodeIt.Commands;

[Command("Dumper", Description = "Generates a dumper zip")]
public class Dumper : ICommand
{
    [CommandParameter(0, IsRequired = true, Description = "The absolute path to your DeObfuscated assembly file, folder must contain all references to be resolved.")]
    public string GameAssemblyPath { get; init; }
    
    [CommandParameter(1, IsRequired = true, Description = "The absolute path to your FileChecker.dll file, folder must contain all refgerences to be resolved.")]
    public string CheckerAssemblyPath { get; init; }
    
    private Dumpy _dumpy { get; set; }
    
    public ValueTask ExecuteAsync(IConsole console)
    {
        DataProvider.IsCli = true;
        DataProvider.LoadAppSettings();
        
        Logger.Log("Creating Dumper...");

        _dumpy = new Dumpy(GameAssemblyPath, CheckerAssemblyPath, Path.GetDirectoryName(GameAssemblyPath));
        _dumpy.CreateDumpFolders();
        _dumpy.CreateDumper();
        
        Logger.Log("Complete", ConsoleColor.Green);
        
        // Wait for log termination
        Logger.Terminate();
        while (Logger.IsRunning()) { }

        return default;
    }
}