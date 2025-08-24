using System.Reflection;
using DumpLib.Models;
using Newtonsoft.Json;

namespace DumpLib.Helpers;

public static class DataHelper
{
    public static Assembly _newtonAssembly = Assembly.LoadFrom(
        (
            Directory.GetCurrentDirectory()
            + "\\EscapeFromTarkov_Data\\Managed\\Newtonsoft.Json.dll"
        ).Replace("\\\\", "\\")
    );

    public static Assembly _msAssembly = Assembly.LoadFrom(
        (
            Directory.GetCurrentDirectory() + "\\EscapeFromTarkov_Data\\Managed\\mscorlib.dll"
        ).Replace("\\\\", "\\")
    );

    public static Assembly _eftAssembly = Assembly.LoadFrom(
        (
            Directory.GetCurrentDirectory()
            + "\\EscapeFromTarkov_Data\\Managed\\Assembly-CSharp.dll"
        ).Replace("\\\\", "\\")
    );

    public static Assembly _comfortAssembly = Assembly.LoadFrom(
        (Directory.GetCurrentDirectory() + "\\EscapeFromTarkov_Data\\Managed\\Comfort.dll").Replace(
            "\\\\",
            "\\"
        )
    );

    public static string DumpDataPath = (Directory.GetCurrentDirectory() + "\\DUMPDATA\\").Replace(
        "\\\\",
        "\\"
    );

    public static SptConfigClass ConfigSettings = GetSptConfig();

    public static string DumpingPath = GetDumpPath();
    public static bool GotBackend = false;
    public static object WaveSettings = null;
    public static object LocalRaidSettings = null;
    public static object RaidSettings = null;
    public static FieldInfo MainMenuController = null;
    public static object Session = null;
    public static object TarkovApp = null;
    public static object LocationValues = null;
    public static object EndRaidClass = null;
    public static object PlayerProfile = null;
    public static int ErrorCounter = 0;

    /// <summary>
    /// Reads and deserializes the SPT configuration from config.json file
    /// </summary>
    /// <returns>Deserialized SPT configuration object</returns>
    public static SptConfigClass GetSptConfig()
    {
        try
        {
            return JsonConvert.DeserializeObject<SptConfigClass>(
                File.ReadAllText(Path.Combine(DumpDataPath, "config.json"))
            );
        }
        catch (Exception e)
        {
            Utils.LogError("GetSptConfig");
            Utils.LogError(e);
            throw;
        }
    }

    /// <summary>
    /// Gets the path where dump files should be stored
    /// </summary>
    /// <returns>Path to dump directory, either custom or default</returns>
    public static string GetDumpPath()
    {
        try
        {
            if (
                ConfigSettings == null
                || !ConfigSettings.EnableCustomDumpPath
                || string.IsNullOrEmpty(ConfigSettings.CustomDumpPath)
            )
            {
                Utils.LogError("CustomDumpPath is empty defaulting to normal pathing");
                return (Directory.GetCurrentDirectory() + "\\HTTP_DATA\\").Replace("\\\\", "\\");
            }

            return ConfigSettings.CustomDumpPath;
        }
        catch (Exception e)
        {
            Utils.LogError("GetCustomDumpPath");
            Utils.LogError(e);
            throw;
        }
    }

    /// <summary>
    /// Loads and deserializes raid configuration settings from raidConfig.json
    /// </summary>
    /// <returns>Deserialized raid configuration object with location settings</returns>
    public static object GetRaidConfigSettings()
    {
        try
        {
            var objectToReturn = ReflectionHelper
                .CreateDeserializerMethod(TypeHelper.GetRaidConfigType())
                .Invoke(
                    null,
                    new[]
                    {
                        File.ReadAllText(Path.Combine(DataHelper.DumpDataPath, "raidConfig.json")),
                    }
                );

            // we now need to attach LocationSettingsClass to _locationSettings in this object - DataHelper.LocationValues should be inited by this point
            return ReflectionHelper.SetLocationSettingsOnRaidSettings(objectToReturn);
        }
        catch (Exception e)
        {
            Utils.LogError("GetRaidConfigSettings");
            Utils.LogError(e);
            throw;
        }
    }

    /// <summary>
    /// Loads and deserializes end raid settings from endRaid.json
    /// </summary>
    /// <returns>Deserialized end raid configuration object</returns>
    public static object GetEndRaidClass()
    {
        try
        {
            return ReflectionHelper
                .CreateDeserializerMethod(TypeHelper.GetEndRaidType())
                .Invoke(
                    null,
                    new[]
                    {
                        File.ReadAllText(Path.Combine(DataHelper.DumpDataPath, "endRaid.json")),
                    }
                );
        }
        catch (Exception e)
        {
            Utils.LogError("GetEndRaidClass");
            Utils.LogError(e);
            throw;
        }
    }

    /// <summary>
    /// Loads and deserializes local raid settings from raidSettings.json
    /// </summary>
    /// <returns>Deserialized local raid settings object</returns>
    public static object GetLocalRaidSettings()
    {
        try
        {
            return ReflectionHelper
                .CreateDeserializerMethod(TypeHelper.GetLocalRaidSettingsType())
                .Invoke(
                    null,
                    new[]
                    {
                        File.ReadAllText(
                            Path.Combine(DataHelper.DumpDataPath, "raidSettings.json")
                        ),
                    }
                );
        }
        catch (Exception e)
        {
            Utils.LogError("GetLocalRaidSettings");
            Utils.LogError(e);
            throw;
        }
    }

    /// <summary>
    /// Resets all static variables to their default values
    /// </summary>
    public static void ClearVariables()
    {
        GotBackend = false;
        WaveSettings = null;
        MainMenuController = null;
        Session = null;
        TarkovApp = null;
        LocalRaidSettings = null;
        RaidSettings = null;
        LocationValues = null;
        EndRaidClass = null;
        PlayerProfile = null;
    }
}
