namespace DumpLib.Helpers;

public static class TypeHelper
{
    /// <summary>
    /// Method to get Singleton<> type from Comfort.dll
    /// </summary>
    /// <returns>Type</returns>
    public static Type GetSingletonType()
    {
        try
        {
            return DataHelper._comfortAssembly.GetTypes().First(x => x.Name.StartsWith("Singleton"));
        }
        catch (Exception e)
        {
            Utils.LogError("GetSingletonType");
            Utils.LogError(e);
            throw;
        }
    }

    /// <summary>
    /// Method to get ClientApplication<> type from EFT's assembly
    /// </summary>
    /// <returns>Type</returns>
    public static Type GetClientApplicationType()
    {
        try
        {
            return DataHelper._eftAssembly.GetTypes().First(x => x.Name.StartsWith("ClientApplication"));
        }
        catch (Exception e)
        {
            Utils.LogError("GetClientApplicationType");
            Utils.LogError(e);
            throw;
        }
    }

    /// <summary>
    /// Method to get (as of 25/01/2024 - GInterface145) type from EFT's assembly
    /// </summary>
    /// <returns>Type</returns>
    public static Type GetInterfaceType()
    {
        try
        {
            return DataHelper._eftAssembly.GetTypes().First(x =>
                x.IsInterface &&
                x.GetMethods().Any(m =>
                    m.Name == "GetPhpSessionId"
                )
            );
        }
        catch (Exception e)
        {
            Utils.LogError("GetInterfaceType");
            Utils.LogError(e);
            throw;
        }
    }

    /// <summary>
    /// Method to get TarkovApplication type from EFT's assembly
    /// </summary>
    /// <returns>Type</returns>
    public static Type GetTarkovApplicationType()
    {
        try
        {
            return DataHelper._eftAssembly.GetTypes().First(x =>
                x.Name == "TarkovApplication"
            );
        }
        catch (Exception e)
        {
            Utils.LogError("GetTarkovApplicationType");
            Utils.LogError(e);
            throw;
        }
    }

    /// <summary>
    /// Method to get (as of 25/01/2024 - GClass1464) type from EFT's assembly
    /// </summary>
    /// <returns></returns>
    public static Type GetWaveSettingsType()
    {
        try
        {
            return DataHelper._eftAssembly.GetTypes().First(x =>
            {
                var fields = x.GetFields();
                if (fields.Any(f => f.Name == "Role") &&
                    fields.Any(f => f.Name == "Limit") &&
                    fields.Any(f => f.Name == "Difficulty")
                    && fields.Length == 3)
                {
                    return true;
                }

                return false;
            });
        }
        catch (Exception e)
        {
            Utils.LogError("GetWaveSettingsType");
            Utils.LogError(e);
            throw;
        }
    }

    public static Type GetListType()
    {
        try
        {
            return DataHelper._msAssembly.GetTypes().First(x =>
                x.Name.StartsWith("List") &&
                x.Namespace == "System.Collections.Generic"
            );
        }
        catch (Exception e)
        {
            Utils.LogError("GetListType");
            Utils.LogError(e);
            throw;
        }
    }

    /// <summary>
    /// Method to get LocalRaidSettings Type from EFT
    /// </summary>
    /// <returns>object</returns>
    public static Type GetLocalRaidSettingsType()
    {
        try
        {
            return DataHelper._eftAssembly.GetTypes().First(x =>
                x.Name == "LocalRaidSettings");
        }
        catch (Exception e)
        {
            Utils.LogError("GetLocalRaidSettingsType");
            Utils.LogError(e);
            throw;
        }
    }

    public static Type GetRaidConfigType()
    {
        try
        {
            return DataHelper._eftAssembly.GetTypes().First(x =>
                x.Name == "RaidSettings");
        }
        catch (Exception e)
        {
            Utils.LogError("GetRaidConfigType");
            Utils.LogError(e);
            throw;
        }
    }

    public static Type GetEndRaidType()
    {
        try
        {
            return DataHelper._eftAssembly.GetTypes().First(x =>
                x.GetFields().Any(f =>
                    f.Name == "killerAid"
                )
            );
        }
        catch (Exception e)
        {
            Utils.LogError("GetEndRaidType");
            Utils.LogError(e);
            throw;
        }
    }

    public static Type GetJsonConverterType()
    {
        try
        {
            return DataHelper._eftAssembly.GetTypes().First(x =>
                x.GetMethods().Any(m =>
                    m.Name == "ToUnparsedData"
                )
            );
        }
        catch (Exception e)
        {
            Utils.LogError("GetJsonConverterType");
            Utils.LogError(e);
            throw;
        }
    }

    public static Type GetProfileType()
    {
        try
        {
            return DataHelper._eftAssembly.GetTypes().First(x =>
                x.Name == "Profile");
        }
        catch (Exception e)
        {
            Utils.LogError("GetProfileType");
            Utils.LogError(e);
            throw;
        }
    }

    // TODO: CLEAN UP REFLECTION
    public static Type GetJsonTokenCreateType()
    {
        try
        {
            return DataHelper._eftAssembly.GetTypes().First(x =>
            {
                var fields = x.GetFields();
                var methods = x.GetMethods();

                return fields.Length == 6 &&
                       fields.Any(f => f.Name == "location") &&
                       fields.Any(f => f.Name == "_id") &&
                       methods.Any(m => m.Name == "Clone") &&
                       methods.Any(m => m.Name == "ToString");
            });
        }
        catch (Exception e)
        {
            Utils.LogError("GetJsonTokenCreateType");
            Utils.LogError(e);
            throw;
        }
    }

    public static Type GetDictionaryType()
    {
        try
        {
            return DataHelper._msAssembly.GetTypes().First(x =>
                x.Name.StartsWith("Dictionary") &&
                x.Namespace == "System.Collections.Generic");
        }
        catch (Exception e)
        {
            Utils.LogError("GetDictionaryType");
            Utils.LogError(e);
            throw;
        }
    }

    public static Type GetProfileShimType()
    {
        try
        {
            return DataHelper._eftAssembly.GetTypes().First(x =>
            {
                var fields = x.GetFields();
                var constructors = x.GetConstructors();
                var properties = x.GetProperties();
                var methods = x.GetMethods();
            
                return fields.Length == 26 
                       && constructors.Length == 2 
                       && properties.Length == 0 
                       && methods.Length == 4
                       && fields.Any(f => f.Name == "Id") 
                       && fields.Any(f => f.Name == "AccountId") 
                       && fields.Any(f => f.Name == "PetId") 
                       && fields.Any(f => f.Name == "KarmaValue") 
                       && fields.Any(f => f.Name == "Customization")
                       && fields.Any(f => f.Name == "Encyclopedia");
            });
        }
        catch (Exception e)
        {
            Utils.LogError("GetProfileShimType");
            Utils.LogError(e);
            throw;
        }
    }

    public static Type GetProfileSearchControllerType()
    {
        try
        {
            return DataHelper._eftAssembly.GetTypes().First(x =>
            {
                var fields = x.GetFields();
                var methods = x.GetMethods();

                return fields.Length == 1 && methods.Length == 17 &&
                       !x.IsInterface && methods.Any(m => m.Name == "IsItemKnown") &&
                       methods.Any(m => m.Name == "TryFindChangedContainer") &&
                       methods.Any(m => m.Name == "GetObserverItemState");
            });
        }
        catch (Exception e)
        {
            Utils.LogError("GetProfileSearchControllerType");
            Utils.LogError(e);
            throw;
        }
    }
}