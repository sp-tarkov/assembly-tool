using AssemblyLib;
using AssemblyLib.Models;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace AssemblyTool.Commands;

[Command(
    "test",
    Description = "Runs a one model test. You will be prompted to enter information upon starting. Useful for testing specific tool features"
)]
public class RunTest : ICommand
{
    [CommandParameter(
        0,
        IsRequired = true,
        Description = "The absolute path to the target dll, folder must contain all references to be resolved"
    )]
    public required string TargetAssemblyPath { get; init; }

    [CommandParameter(1, IsRequired = true, Description = "The absolute path to the dummy dll")]
    public required string DummyDllPath { get; init; }

    public ValueTask ExecuteAsync(IConsole console)
    {
        var (target, model) = GatherTestInfo(console);
        if (target is null || model is null)
        {
            return ValueTask.CompletedTask;
        }

        var app = new App();
        app.RunDirectMapTest(TargetAssemblyPath, DummyDllPath, target, model);

        return ValueTask.CompletedTask;
    }

    private (string?, DirectMapModel?) GatherTestInfo(IConsole console)
    {
        console.WriteLine("Enter target GClass name to target");
        var target = console.ReadLine();
        if (string.IsNullOrEmpty(target))
        {
            console.WriteLine("Cannot target an empty string.");
            return (null, null);
        }

        console.WriteLine("Enter a new name for the target");
        var newName = console.ReadLine();
        if (string.IsNullOrEmpty(newName))
        {
            console.WriteLine("New name cannot be empty.");
            return (null, null);
        }

        console.WriteLine("Enter a new namespace, leave blank to skip");
        var newNamespace = console.ReadLine();

        return (
            target,
            new DirectMapModel { NewName = newName, NewNamespace = newNamespace?.Length > 0 ? newNamespace : null }
        );
    }
}
