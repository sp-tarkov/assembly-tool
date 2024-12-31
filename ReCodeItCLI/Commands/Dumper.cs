using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ReCodeItLib.Utils;
using ReCodeItLib.Dumper;

namespace ReCodeItCLI.Commands;

[Command("Dumper", Description = "Generates a dumper zip")]
public class Dumper : ICommand
{
    [CommandParameter(0, IsRequired = true, Description = "The absolute path to your Managed folder for EFT, folder must contain all references to be resolved. Assembly-CSharp-cleaned.dll, mscorlib.dll, FilesChecker.dll")]
    public required string ManagedDirectory { get; init; }
    
    public ValueTask ExecuteAsync(IConsole console)
    {
        DataProvider.IsCli = true;
        DataProvider.LoadAppSettings();
        
        Logger.Log("Creating DumperClass...");

        var dumper = new DumperClass(ManagedDirectory);
        dumper.CreateDumpFolders();
        dumper.CreateDumper();
        dumper.CopyFiles();
        dumper.ZipFiles();
        
        Logger.Log("Complete", ConsoleColor.Green);
        
        // Wait for log termination
        Logger.Terminate();
        while (Logger.IsRunning()) { }

        return default;
    }
}