using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ReCodeItLib.Utils;

namespace ReCodeItCLI.Commands;

[Command("GetRuntimeVersion", Description = "Prints out the .net runtime version this assembly targets")]
public class GetRuntimeVersion : ICommand
{
	[CommandParameter(0, IsRequired = true, Description = "The absolute path to your dll.")]
	public required string AssemblyPath { get; init; }
	
	public ValueTask ExecuteAsync(IConsole console)
	{
		var module = DataProvider.LoadModule(AssemblyPath);
		
		Logger.LogSync($"Target Runtime Version: {module.RuntimeVersion}");

		return default;
	}
}