using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ReCodeItLib.Models;
using ReCodeItLib.ReMapper;
using ReCodeItLib.Utils;

namespace ReCodeItCLI.Commands;

[Command("AutoMatch", Description = "Automatically tries to generate a mapping object with the provided arguments.")]
public class AutoMatchCommand : ICommand
{
	[CommandParameter(0, IsRequired = true, Description = "The absolute path to your obfuscated assembly or exe file, folder must contain all references to be resolved.")]
	public required string AssemblyPath { get; init; }
	
	[CommandParameter(1, IsRequired = true, Description = "Full old type name including namespace")]
	public required string OldTypeName { get; init; }
	
	[CommandParameter(2, IsRequired = true, Description = "The name you want the type to be renamed to")]
	public required string NewTypeName { get; init; }

	[CommandParameter(3, IsRequired = false, Description = "Path to your mapping file so it can be updated if a match is found")]
	public string MappingsPath { get; init; }

	public ValueTask ExecuteAsync(IConsole console)
	{
		DataProvider.IsCli = true;
		DataProvider.LoadAppSettings();
		
		Logger.LogSync("Finding match...");

		var remaps = new List<RemapModel>();
		
		if (!string.IsNullOrEmpty(MappingsPath))
		{
			Logger.LogSync("Loaded mapping file", ConsoleColor.Green);
			remaps.AddRange(DataProvider.LoadMappingFile(MappingsPath));
		}
		
		new AutoMatcher(remaps, MappingsPath)
			.AutoMatch(AssemblyPath, OldTypeName, NewTypeName);
		
		// Wait for log termination
		Logger.Terminate();
		while(Logger.IsRunning()) {}
        
		return default;
	}
}