// Uncomment this to have the application wait for a debugger to attach before running.
#define WAIT_FOR_DEBUGGER

using System.Diagnostics;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ReCodeItLib.Models;
using ReCodeItLib.ReMapper;
using ReCodeItLib.Utils;

namespace ReCodeItCLI.Commands;

[Command("AutoMatch", Description = "This command will automatically try to generate a mapping object given old type and new type names.")]
public class AutoMatchCommand : ICommand
{
	[CommandParameter(0, IsRequired = true, Description = "The absolute path to your assembly, folder must contain all references to be resolved.")]
	public required string AssemblyPath { get; init; }
	
	[CommandParameter(1, IsRequired = true, Description = "Path to your mapping file so it can be updated if a match is found")]
	public string MappingsPath { get; init; }
	
	[CommandParameter(2, IsRequired = true, Description = "Full old type name including namespace")]
	public required string OldTypeName { get; init; }
	
	[CommandParameter(3, IsRequired = true, Description = "The name you want the type to be renamed to")]
	public required string NewTypeName { get; init; }


	public ValueTask ExecuteAsync(IConsole console)
	{
		Logger.LogSync("Finding match...");

		var remaps = new List<RemapModel>();

#if WAIT_FOR_DEBUGGER
		Logger.LogSync("Waiting for debugger...");
		while (!Debugger.IsAttached)
		{
			Thread.Sleep(100);
		}
#endif
		
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