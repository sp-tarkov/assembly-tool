using AssemblyLib;
using AssemblyLib.AutoMatcher;
using AssemblyLib.Utils;
using AssemblyTool.Utils;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace AssemblyTool.Commands;

[Command("regensig", Description = "regenerates the signature of a mapping if it is failing")]
public class RegenerateSignature : CliFx.ICommand
{
    [CommandParameter(
        0,
        IsRequired = true,
        Description = "The absolute path to the assembly you want to regenerate the signature for"
    )]
    public required string AssemblyPath { get; init; }

    [CommandParameter(
        1,
        IsRequired = true,
        Description = "Full old type name including namespace `Foo.Bar` for nested classes `Foo.Bar/FooBar`"
    )]
    public required string OldTypeName { get; init; }

    [CommandParameter(2, IsRequired = true, Description = "The new type name as listed in the mapping file")]
    public required string NewTypeName { get; init; }

    [CommandParameter(
        3,
        IsRequired = false,
        Description = "The absolute path to the previous assembly. This is used for generating meta data for custom attributes."
    )]
    public string? OldAssemblyPath { get; init; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        Debugger.TryWaitForDebuggerAttach();

        var app = new App();
        await app.RunAutoMatcher(AssemblyPath, OldAssemblyPath!, OldTypeName, NewTypeName, true);
    }
}
