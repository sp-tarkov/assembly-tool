using ReCodeItLib.Utils;

namespace ReCodeItLib.Models;

/// <summary>
/// All settings container
/// </summary>
public class Settings
{
    /// <summary>
    /// Path to the mapping file
    /// </summary>
    public string MappingPath { get; set; } = string.Empty;
    
    /// <summary>
    /// The re-mapper will look for these tokens in class names, otherwise they will be skipped
    /// </summary>
    public required List<string> TokensToMatch { get; set; }
}