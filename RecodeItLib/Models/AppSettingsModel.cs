using ReCodeIt.Utils;

namespace ReCodeIt.Models;

/// <summary>
/// All settings container
/// </summary>
public class Settings
{
    private AppSettings? _appSettings;

    public AppSettings? AppSettings
    {
        get { return _appSettings; }
        set
        {
            _appSettings = value;
            Save();
        }
    }

    private RemapperSettings? _remapper;

    public RemapperSettings? Remapper
    {
        get { return _remapper; }
        set
        {
            _remapper = value;
            Save();
        }
    }
    
    private void Save()
    {
        DataProvider.SaveAppSettings();
    }
}

/// <summary>
/// These are settings for the application
/// </summary>
public class AppSettings
{
    private bool _debug;

    public bool Debug
    {
        get { return _debug; }
        set
        {
            _debug = value;
            Save();
        }
    }

    private bool _silentMode;

    public bool SilentMode
    {
        get { return _silentMode; }
        set
        {
            _silentMode = value;
            Save();
        }
    }

    private void Save()
    {
        DataProvider.SaveAppSettings();
    }
}

/// <summary>
/// These are settings for the manual remapper
/// </summary>
public class RemapperSettings
{
    private string _assemblyPath = string.Empty;

    /// <summary>
    /// Path to the assembly we want to remap
    /// </summary>
    public string AssemblyPath
    {
        get { return _assemblyPath; }
        set
        {
            _assemblyPath = value;
            Save();
        }
    }

    private string _outputPath = string.Empty;

    /// <summary>
    /// Path including the filename and extension we want to write the changes to
    /// </summary>
    public string OutputPath
    {
        get { return _outputPath; }
        set
        {
            _outputPath = value;
            Save();
        }
    }

    private string _mappingPath = string.Empty;

    /// <summary>
    /// Path to the mapping file
    /// </summary>
    public string MappingPath
    {
        get { return _mappingPath; }
        set
        {
            _mappingPath = value;
            Save();
        }
    }

    private bool _useProjectMappings;

    /// <summary>
    /// Use the projects mappings instead of a standalone file
    /// </summary>
    public bool UseProjectMappings
    {
        get { return _useProjectMappings; }
        set
        {
            _useProjectMappings = value;
            Save();
        }
    }

    private MappingSettings? _mappingSettings;

    public MappingSettings? MappingSettings
    {
        get { return _mappingSettings; }
        set
        {
            _mappingSettings = value;
            Save();
        }
    }

    private List<string> _tokensToMatch = [];

    /// <summary>
    /// The re-mapper will look for these tokens in class names, otherwise they will be skipped
    /// </summary>
    public List<string> TokensToMatch
    {
        get { return _tokensToMatch; }
        set
        {
            _tokensToMatch = value;
            Save();
        }
    }
    
    private void Save()
    {
        DataProvider.SaveAppSettings();
    }
}

/// <summary>
/// These are settings that all versions of the remappers use
/// </summary>
public class MappingSettings
{
    private bool _renameFields;

    /// <summary>
    /// Names of fields of the matched type will be renamed to the type name with approproiate convention
    /// </summary>
    public bool RenameFields
    {
        get { return _renameFields; }
        set
        {
            _renameFields = value;
            Save();
        }
    }

    private bool _renameProps;

    /// <summary>
    /// Names of properties of the matched type will be renamed to the type name with approproiate convention
    /// </summary>
    public bool RenameProperties
    {
        get { return _renameProps; }
        set
        {
            _renameProps = value;
            Save();
        }
    }

    private bool _publicize;

    /// <summary>
    /// Publicize all types, methods, and properties : NOTE: Not run until after the remap has completed
    /// </summary>
    public bool Publicize
    {
        get { return _publicize; }
        set
        {
            _publicize = value;
            Save();
        }
    }

    private bool _unseal;

    /// <summary>
    /// Unseal all types : NOTE: Not run until after the remap has completed
    /// </summary>
    public bool Unseal
    {
        get { return _unseal; }
        set
        {
            _unseal = value;
            Save();
        }
    }

    private void Save()
    {
        DataProvider.SaveAppSettings();
    }
}