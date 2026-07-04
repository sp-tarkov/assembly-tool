// See https://aka.ms/new-console-template for more information
using CliFx;

namespace AssemblyTool;

public static class Program
{
    public static async Task<int> Main()
    {
        return await new CliApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .AllowDebugMode()
            .SetExecutableName("AssemblyTool")
            .Build()
            .RunAsync();
    }
}
