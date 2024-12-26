using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ReCodeIt.Utils;
using ReCodeItLib.Remapper;

namespace ReCodeItCLI.Commands;

[Command("DeObfuscate", Description = "Generates a de-obfuscated -cleaned dll in the folder your assembly is in")]
public class DeObfuscate : ICommand
{
    [CommandParameter(0, IsRequired = true, Description = "The absolute path to your obfuscated assembly or exe file, folder must contain all references to be resolved.")]
    public string AssemblyPath { get; init; }

    [CommandParameter(1, IsRequired = false, Description = "Is the target the EFT launcher?")]
    public bool IsLauncher { get; init; } = false;

    public ValueTask ExecuteAsync(IConsole console)
    {
        DataProvider.IsCli = true;
        DataProvider.LoadAppSettings();

        Logger.Log("Deobfuscating assembly...");

        Deobfuscator.Deobfuscate(AssemblyPath, IsLauncher);

        Logger.Log("Complete", ConsoleColor.Green);

        // Wait for log termination
        Logger.Terminate();
        while(Logger.IsRunning()) {}
        
        return default;
    }
}