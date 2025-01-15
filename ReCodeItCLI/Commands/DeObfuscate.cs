using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ReCodeItCLI.Utils;
using ReCodeItLib.Utils;
using ReCodeItLib.ReMapper;

namespace ReCodeItCLI.Commands;

[Command("DeObfuscate", Description = "Generates a de-obfuscated -cleaned dll in the folder your assembly is in")]
public class DeObfuscate : ICommand
{
    [CommandParameter(0, IsRequired = true, Description = "The absolute path to your obfuscated assembly or exe file, folder must contain all references to be resolved.")]
    public required string AssemblyPath { get; init; }

    [CommandParameter(1, IsRequired = false, Description = "Is the target the EFT launcher?")]
    public bool IsLauncher { get; init; } = false;

    public ValueTask ExecuteAsync(IConsole console)
    {
        Debugger.TryWaitForDebuggerAttach();
        
        Logger.Log("Deobfuscating assembly...");

        Deobfuscator.Deobfuscate(AssemblyPath, IsLauncher);

        Logger.Log("Complete", ConsoleColor.Green);

        // Wait for log termination
        Logger.Terminate();
        while(Logger.IsRunning()) {}
        
        return default;
    }
}