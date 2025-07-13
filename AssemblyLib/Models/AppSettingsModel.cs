namespace AssemblyLib.Models;

/// <summary>
/// All settings container
/// </summary>
public class Settings
{
    /// <summary>
    /// Path to the games root directory
    /// </summary>
    public string GamePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Should we install the created dll to the game?
    /// </summary>
    public bool CopyToGame { get; set; }
    
    /// <summary>
    /// Path to the modules project root directory
    /// </summary>
    public string ModulesProjectPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Should we copy the hollowed dll to the project directory?
    /// </summary>
    public bool CopyToModules { get; set; }
    
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