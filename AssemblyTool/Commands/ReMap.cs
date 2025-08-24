﻿using AssemblyLib;
using AssemblyTool.Utils;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace AssemblyTool.Commands;

[Command(
    "ReMap",
    Description = "Generates a re-mapped dll provided a mapping file and dll. If the dll is obfuscated, it will automatically de-obfuscate."
)]
public class ReMap : ICommand
{
    [CommandParameter(
        0,
        IsRequired = true,
        Description = "The absolute path to your dll, containing all references that it needs to resolve."
    )]
    public required string TargetAssemblyPath { get; init; }

    [CommandParameter(
        1,
        IsRequired = false,
        Description = "The absolute path to the previous assembly. This is used for generating meta data for custom attributes."
    )]
    public string? OldAssemblyPath { get; init; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        Debugger.TryWaitForDebuggerAttach();

        var outPath = Path.GetDirectoryName(TargetAssemblyPath);

        if (outPath is null)
        {
            throw new DirectoryNotFoundException("OutPath could not be resolved.");
        }

        var app = new App();
        await app.RunRemapProcess(TargetAssemblyPath, OldAssemblyPath, outPath);
    }
}
