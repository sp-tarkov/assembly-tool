using System.Text.Json.Serialization;
using AsmResolver.DotNet;

namespace AssemblyLib.Models;

public record DirectMapModel
{
    [JsonIgnore]
    public TypeDefinition? Type { get; set; }

    public string? NewName { get; init; }
    public string? NewNamespace { get; init; }

    public Dictionary<string, string>? MethodRenames { get; init; }
    public Dictionary<string, string>? PropertyRenames { get; init; }
    public Dictionary<string, string>? FieldRenames { get; init; }

    public Dictionary<string, DirectMapModel>? NestedTypes { get; init; }
}
