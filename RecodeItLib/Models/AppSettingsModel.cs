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
    /// The re-mapper will look for these classes, otherwise they will be skipped
    /// </summary>
    public required List<string> TypeNamesToMatch { get; set; }

    /// <summary>
    /// List of method names to be ignored during the auto-match process.
    /// </summary>
    public required List<string> MethodsToIgnore { get; set; }
}