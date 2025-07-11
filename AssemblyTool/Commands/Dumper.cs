using AssemblyLib;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using AssemblyLib.Utils;
using AssemblyLib.Dumper;
using AssemblyTool.Utils;

namespace AssemblyTool.Commands;

[Command("Dumper", Description = "Generates a dumper zip")]
public class Dumper : ICommand
{
    [CommandParameter(0, IsRequired = true, Description = "The absolute path to your Managed folder for EFT, folder must contain all references to be resolved. Assembly-CSharp-cleaned.dll, mscorlib.dll, FilesChecker.dll")]
    public required string ManagedDirectory { get; init; }

    public ValueTask ExecuteAsync(IConsole console)
    {
        Debugger.TryWaitForDebuggerAttach();

        Logger.Log("Creating DumperClass...");
        
        var app = new App();
        app.CreateDumper(ManagedDirectory);
        
        Logger.Log("Complete", ConsoleColor.Green);

        return default;
    }
}