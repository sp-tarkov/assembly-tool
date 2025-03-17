using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ReCodeItCLI.Utils;
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
	
	[CommandParameter(2, IsRequired = true, Description = "Full old type name including namespace `Foo.Bar` for nested classes `Foo.Bar/FooBar`")]
	public required string OldTypeName { get; init; }
	
	[CommandParameter(3, IsRequired = true, Description = "The name you want the type to be renamed to")]
	public required string NewTypeName { get; init; }
	
	[CommandParameter(4, IsRequired = false, Description = "The absolute path to the previous assembly. This is used for generating meta data for custom attributes.")]
	public string? OldAssemblyPath { get; init; }
	
	public ValueTask ExecuteAsync(IConsole console)
	{
		Debugger.TryWaitForDebuggerAttach();
		
		Logger.Log("Finding match...");

		var remaps = new List<RemapModel>();
		
		if (!string.IsNullOrEmpty(MappingsPath))
		{
			Logger.Log("Loaded mapping file", ConsoleColor.Green);
			remaps.AddRange(DataProvider.LoadMappingFile(MappingsPath));
		}
		
		new AutoMatcher(remaps, MappingsPath)
			.AutoMatch(AssemblyPath, OldAssemblyPath!, OldTypeName, NewTypeName);
		
		return default;
	}
}