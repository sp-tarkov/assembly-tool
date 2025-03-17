// See https://aka.ms/new-console-template for more information
using CliFx;

namespace ReCodeItCLI;

public static class Program
{
    public static async Task<int> Main() =>
        await new CliApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .AllowDebugMode() 
            .SetExecutableName("ReCodeIt")
            .Build()
            .RunAsync();
}