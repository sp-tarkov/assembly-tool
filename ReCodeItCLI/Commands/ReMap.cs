// Uncomment this to have the application wait for a debugger to attach before running.
//#define WAIT_FOR_DEBUGGER

using System.Diagnostics;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ReCodeItLib.Utils;
using ReCodeItLib.ReMapper;

namespace ReCodeItCLI.Commands;

[Command("ReMap", Description = "Generates a re-mapped dll provided a mapping file and dll. If the dll is obfuscated, it will automatically de-obfuscate.")]
public class ReMap : ICommand
{
    private ReMapper _remapper { get; set; } = new();

    [CommandParameter(0, IsRequired = true, Description = "The absolute path to your mapping.json file, supports .json and .jsonc")]
    public required string MappingJsonPath { get; init; }

    [CommandParameter(1, IsRequired = true, Description = "The absolute path to your dll, containing all references that it needs to resolve.")]
    public required string AssemblyPath { get; init; }
    
    public ValueTask ExecuteAsync(IConsole console)
    {
#if WAIT_FOR_DEBUGGER
		Logger.LogSync("Waiting for debugger...");
		while (!Debugger.IsAttached)
		{
			Thread.Sleep(100);
		}
#endif
        
        DataProvider.Settings.MappingPath = MappingJsonPath;

        var remaps = DataProvider.LoadMappingFile(MappingJsonPath);

        var outPath = Path.GetDirectoryName(AssemblyPath);

        if (outPath is null)
        {
            throw new DirectoryNotFoundException("OutPath could not be resolved.");
        }
        
        _remapper.InitializeRemap(remaps, AssemblyPath, outPath);

        // Wait for log termination
        Logger.Terminate();
        while(Logger.IsRunning()) {}
        
        return default;
    }
}