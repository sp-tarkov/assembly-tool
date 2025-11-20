using AssemblyLib.Shared;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace AssemblyTool.Commands;

[Command(
    "DirectMap",
    Description = "Generates direct-mapped dll provided a target dll. If the dll is obfuscated, it will automatically de-obfuscate."
)]
public class DirectMap : ICommand
{
    [CommandParameter(
        0,
        IsRequired = true,
        Description = "The absolute path to the target dll, folder must contain all references to be resolved"
    )]
    public required string TargetAssemblyPath { get; init; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var app = new App();
        await app.RunDirectMapProcess(TargetAssemblyPath);
    }
}
