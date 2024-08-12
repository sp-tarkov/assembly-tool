using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DumpLib.Helpers
{
    public static class ReflectionHelper
    {
        private static Assembly _newtonAssembly = Assembly.LoadFrom((Directory.GetCurrentDirectory() + "\\EscapeFromTarkov_Data\\Managed\\Newtonsoft.Json.dll").Replace("\\\\", "\\"));

        private static Assembly _msAssembly = Assembly.LoadFrom((Directory.GetCurrentDirectory() + "\\EscapeFromTarkov_Data\\Managed\\mscorlib.dll").Replace("\\\\", "\\"));

        private static Assembly _eftAssembly = Assembly.LoadFrom((Directory.GetCurrentDirectory() + "\\EscapeFromTarkov_Data\\Managed\\Assembly-CSharp.dll").Replace("\\\\", "\\"));

        private static Assembly _comfortAssembly = Assembly.LoadFrom((Directory.GetCurrentDirectory() + "\\EscapeFromTarkov_Data\\Managed\\Comfort.dll").Replace("\\\\", "\\"));

        /// <summary>
        /// Method to get Singleton<> type from Comfort.dll
        /// </summary>
        /// <returns>Type</returns>
        public static Type GetSingletonType()
        {
            try
            {
                return _comfortAssembly.GetTypes().First(x => x.Name.StartsWith("Singleton"));
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetSingletonType");
                UtilsHelper.LogError(e);
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
                return _eftAssembly.GetTypes().First(x => x.Name.StartsWith("ClientApplication"));
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetClientApplicationType");
                UtilsHelper.LogError(e);
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
                return _eftAssembly.GetTypes()
                    .First(x => x.IsInterface && x.GetMethods().Any(m => m.Name == "GetPhpSessionId"));
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetInterfaceType");
                UtilsHelper.LogError(e);
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
                return _eftAssembly.GetTypes().First(x => x.Name == "TarkovApplication");
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetTarkovApplicationType");
                UtilsHelper.LogError(e);
                throw;
            }
        }

        /// <summary>
        /// Method to get (as of 25/01/2024 - GClass1464) type from EFT's assembly
        /// </summary>
        /// <returns></returns>
        public static object GetWaveSettingsType()
        {
            try
            {
                return _eftAssembly.GetTypes().First(x =>
                {
                    // if type contains Role, Limit and Difficulty, return true
                    var fields = x.GetFields();
                    if (fields.Any(f => f.Name == "Role") && fields.Any(f => f.Name == "Limit") && fields.Any(f => f.Name == "Difficulty") && fields.Length == 3)
                        return true;

                    return false;
                });
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetWaveSettingsType");
                UtilsHelper.LogError(e);
                throw;
            }
        }

        public static Type GetListType()
        {
            try
            {
                return _msAssembly.GetTypes().First(x => x.Name.StartsWith("List") && x.Namespace == "System.Collections.Generic");
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetListType");
                UtilsHelper.LogError(e);
                throw;
            }
        }

        /// <summary>
        /// Method to get FieldInfo of a field on the TarkovApplication Type for later use
        /// </summary>
        /// <returns>FieldInfo</returns>
        public static FieldInfo GetMainMenuControllerField()
        {
            try
            {
                return GetTarkovApplicationType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .First(x => x.FieldType.GetMethods().Any(m => m.Name == "StopAfkMonitor"));
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetMainMenuControllerField");
                UtilsHelper.LogError(e);
                throw;
            }
        }

        /// <summary>
        /// Method to get the Instance of a Singleton(Type) passed in
        /// </summary>
        /// <param name="singletonType">object (Type)</param>
        /// <param name="instance">object (Type)</param>
        /// <returns>object</returns>
        public static object GetSingletonInstance(object singletonType)
        {
            try
            {
                return (singletonType as Type).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    .GetGetMethod().Invoke(singletonType, null);
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetSingletonInstance");
                UtilsHelper.LogError(e);
                throw;
            }
        }

        /// <summary>
        /// Method to get BackendSession object from the instance passed in
        /// </summary>
        /// <param name="instance">object (Type)</param>
        /// <returns>object</returns>
        public static object GetBackendSession(object instance)
        {
            try
            {
                return GetTarkovApplicationType().GetMethod("GetClientBackEndSession").Invoke(instance, null);
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetBackendSession");
                UtilsHelper.LogError(e);
                throw;
            }
        }

        /// <summary>
        /// Method to get DeserializeObject from Newtonsoft assembly
        /// </summary>
        /// <returns>MethodInfo</returns>
        public static MethodInfo GetDeserializerMethodInfo()
        {
            try
            {
                return _newtonAssembly.GetTypes().First(x => x.Name == "JsonConvert").GetMethods().First(m =>
                    m.Name == "DeserializeObject" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1 &&
                    m.GetParameters().Any(p => p.ParameterType == typeof(string)));
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetDeserializerMethodInfo");
                UtilsHelper.LogError(e);
                throw;
            }
        }

        /// <summary>
        /// Method to get Quit method from EFT (as of 20/05/2024 - GClass1955)
        /// </summary>
        /// <returns>MethodInfo</returns>
        public static MethodInfo GetApplicationQuitMethod()
        {
            try
            {
                return _eftAssembly.GetTypes().First(x => x.GetMethods().Any(y => y.Name == "Quit")).GetMethod("Quit");
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetApplicationQuitMethod");
                UtilsHelper.LogError(e);
                throw;
            }
        }

        /// <summary>
        /// Method to get LocalRaidSettings Type from EFT
        /// </summary>
        /// <returns>object</returns>
        public static object GetLocalRaidSettingsType()
        {
            try
            {
                return _eftAssembly.GetTypes().First(x => x.Name == "LocalRaidSettings");
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetLocalRaidSettingsType");
                UtilsHelper.LogError(e);
                throw;
            }
        }

        public static object GetRaidSettingsFromApp(object tarkovApp)
        {
            try
            {
                return tarkovApp.GetType().GetField("_raidSettings").GetValue(tarkovApp);
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetRaidSettingsFromApp");
                UtilsHelper.LogError(e);
                throw;
            }
        }
    }
}