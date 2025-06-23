using AssemblyLib.AutoMatcher;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using AssemblyTool.Utils;

namespace AssemblyTool.Commands;

[Command("AutoMatch", Description = "This command will automatically try to generate a mapping object given old type and new type names.")]
public class AutoMatchCommand : ICommand
{
	[CommandParameter(0, IsRequired = true, Description = "The absolute path to your assembly, folder must contain all references to be resolved.")]
	public required string AssemblyPath { get; init; }
	
	[CommandParameter(1, IsRequired = true, Description = "Full old type name including namespace `Foo.Bar` for nested classes `Foo.Bar/FooBar`")]
	public required string OldTypeName { get; init; }
	
	[CommandParameter(2, IsRequired = true, Description = "The name you want the type to be renamed to")]
	public required string NewTypeName { get; init; }
	
	[CommandParameter(3, IsRequired = false, Description = "The absolute path to the previous assembly. This is used for generating meta data for custom attributes.")]
	public string? OldAssemblyPath { get; init; }
	
	public async ValueTask ExecuteAsync(IConsole console)
	{
		Debugger.TryWaitForDebuggerAttach();
		await new AutoMatcher(false).AutoMatch(AssemblyPath, OldAssemblyPath!, OldTypeName, NewTypeName);
	}
}