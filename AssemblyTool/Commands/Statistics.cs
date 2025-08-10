using AssemblyLib;
using AssemblyTool.Utils;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace AssemblyTool.Commands;

[Command("statistics", Description = "Generates statistics related to the assembly.")]
public class Statistics : ICommand
{
    [CommandParameter(0, IsRequired = true, Description = "The absolute path to your de-obfuscated and remapped dll")]
    public required string TargetAssemblyPath { get; init; }
    
    public async ValueTask ExecuteAsync(IConsole console)
    {
        Debugger.TryWaitForDebuggerAttach();
        
        var app = new App();
        await app.RunStatistics(TargetAssemblyPath);
    }
}