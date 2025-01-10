using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ReCodeItLib.Utils;

namespace ReCodeItCLI.Commands;

[Command("AddMissingProperties", Description = "[DEVELOPMENT COMMAND] This command will add missing properties to the provided mapping.json.")]
public class AddMissingProperties : ICommand
{
	[CommandParameter(0, IsRequired = true, Description = "Path to the mapping.json file to be fixed")]
	public string MappingsPath { get; init; }
	
	public ValueTask ExecuteAsync(IConsole console)
	{
		var remaps = DataProvider.LoadMappingFile(MappingsPath);
		DataProvider.UpdateMapping(MappingsPath, remaps);
		
		Logger.LogSync("Successfully updated mapping file");
        
		return default;
	}
}