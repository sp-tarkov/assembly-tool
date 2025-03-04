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
    public required List<string> MethodNamesToIgnore { get; set; }
    
    /// <summary>
    /// List of method names to be ignored during the auto-match process.
    /// </summary>
    public required List<string> FieldNamesToIgnore { get; set; }
    
    /// <summary>
    /// Pairs of item overrides used for the TemplateIdToObjectMappingClass matching process
    /// </summary>
    public required Dictionary<string, string> ItemObjectIdOverrides { get; set; }
    
    /// <summary>
    /// Pairs of template overrides used for the TemplateIdToObjectMappingClass matching process
    /// </summary>
    public required Dictionary<string, string> TemplateObjectIdOverrides { get; set; }
}