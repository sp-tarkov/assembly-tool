using AssemblyLib;
using AssemblyLib.ReMapper;
using AssemblyLib.Utils;
using AssemblyTool.Utils;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace AssemblyTool.Commands;

[Command("DeObfuscate", Description = "Generates a de-obfuscated -cleaned dll in the folder your assembly is in")]
public class DeObfuscate : ICommand
{
    [CommandParameter(
        0,
        IsRequired = true,
        Description = "The absolute path to your obfuscated assembly or exe file, folder must contain all references to be resolved."
    )]
    public required string AssemblyPath { get; init; }

    [CommandParameter(1, IsRequired = false, Description = "Is the target the EFT launcher?")]
    public bool IsLauncher { get; init; } = false;

    public ValueTask ExecuteAsync(IConsole console)
    {
        Debugger.TryWaitForDebuggerAttach();

        var app = new App();
        app.DeObfuscate(AssemblyPath, IsLauncher);

        return default;
    }
}
