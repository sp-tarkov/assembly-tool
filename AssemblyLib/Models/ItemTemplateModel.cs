using System.Text.Json.Serialization;

namespace AssemblyLib.Models;

public class ItemTemplateModel
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("_name")]
    public string? Name { get; set; }
}
