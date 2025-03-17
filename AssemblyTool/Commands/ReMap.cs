using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ReCodeItCLI.Utils;
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
    public required string TargetAssemblyPath { get; init; }
    
    [CommandParameter(2, IsRequired = false, Description = "The absolute path to the previous assembly. This is used for generating meta data for custom attributes.")]
    public string? OldAssemblyPath { get; init; }
    
    public ValueTask ExecuteAsync(IConsole console)
    {
	    Debugger.TryWaitForDebuggerAttach();
        
        DataProvider.Settings.MappingPath = MappingJsonPath;

        var remaps = DataProvider.LoadMappingFile(MappingJsonPath);

        var outPath = Path.GetDirectoryName(TargetAssemblyPath);

        if (outPath is null)
        {
            throw new DirectoryNotFoundException("OutPath could not be resolved.");
        }
        
        _remapper.InitializeRemap(remaps, TargetAssemblyPath, OldAssemblyPath, outPath);
        
        return default;
    }
}