using ReCodeItLib.Utils;

namespace ReCodeItLib.Models;

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