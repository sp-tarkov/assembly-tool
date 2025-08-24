using AssemblyLib;
using AssemblyLib.Dumper;
using AssemblyLib.Utils;
using AssemblyTool.Utils;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace AssemblyTool.Commands;

[Command("Dumper", Description = "Generates a dumper zip")]
public class Dumper : ICommand
{
    [CommandParameter(
        0,
        IsRequired = true,
        Description = "The absolute path to your Managed folder for EFT, folder must contain all references to be resolved. Assembly-CSharp-cleaned.dll, mscorlib.dll, FilesChecker.dll"
    )]
    public required string ManagedDirectory { get; init; }

    public ValueTask ExecuteAsync(IConsole console)
    {
        Debugger.TryWaitForDebuggerAttach();

        var app = new App();
        app.CreateDumper(ManagedDirectory);

        return default;
    }
}
