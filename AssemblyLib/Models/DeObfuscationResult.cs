using AsmResolver.DotNet;

namespace AssemblyLib.Models;

public record DeObfuscationResult
{
    public bool Success { get; set; }
    public string? DeObfuscatedAssemblyPath { get; set; }

    public ModuleDefinition? DeObfuscatedModule { get; set; }
}
