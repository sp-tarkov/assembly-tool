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
        DataProvider.IsCli = true;
        DataProvider.LoadAppSettings();
        DataProvider.Settings.Remapper.MappingPath = MappingJsonPath;

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