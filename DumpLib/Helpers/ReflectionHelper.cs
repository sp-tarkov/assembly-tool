using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace DumpLib.Helpers
{
    public static class ReflectionHelper
    {
        /// <summary>
        /// Method to get FieldInfo of a field on the TarkovApplication Type for later use
        /// </summary>
        /// <returns>FieldInfo</returns>
        public static FieldInfo GetMainMenuControllerField()
        {
            try
            {
                return TypeHelper.GetTarkovApplicationType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .First(x => x.FieldType.GetMethods().Any(m => m.Name == "StopAfkMonitor"));
            }
            catch (Exception e)
            {
                Utils.LogError("GetMainMenuControllerField");
                Utils.LogError(e);
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
                Utils.LogError("GetSingletonInstance");
                Utils.LogError(e);
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
                return TypeHelper.GetTarkovApplicationType().GetMethod("GetClientBackEndSession").Invoke(instance, null);
            }
            catch (Exception e)
            {
                Utils.LogError("GetBackendSession");
                Utils.LogError(e);
                throw;
            }
        }

        /// <summary>
        /// <para>Method to create a "combined" Type that takes a GenericType</para>
        /// <para>Example: ClientApplication + GInterface145 = ClientApplication(GInterface145)</para>
        /// </summary>
        /// <param name="firstType">Object (Type)</param>
        /// <param name="secondType">Object (Type)</param>
        /// <returns>Type</returns>
        public static Type CreateGenericType(object firstType, object secondType)
        {
            try
            {
                return (firstType as Type).MakeGenericType(new Type[] { secondType as Type });
            }
            catch (Exception e)
            {
                Utils.LogError("CreateGenericType1");
                Utils.LogError(e);
                throw;
            }
        }

        public static Type CreateGenericType(object firstType, object secondType, object thirdType)
        {
            try
            {
                return (firstType as Type).MakeGenericType(new Type[] { secondType as Type, thirdType as Type });
            }
            catch (Exception e)
            {
                Utils.LogError("CreateGenericType2");
                Utils.LogError(e);
                throw;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static MethodInfo CreateDeserializerMethod(object type)
        {
            try
            {
                return MethodHelper.GetDeserializerMethodInfo().MakeGenericMethod(new Type[] { type as Type });
            }
            catch (Exception e)
            {
                Utils.LogError("CreateDeserializerMethod");
                Utils.LogError(e);
                throw;
            }
        }

        public static MethodInfo CreateGenericMethod(MethodInfo method, object type)
        {
            try
            {
                return method.MakeGenericMethod(new Type[] { type as Type });
            }
            catch (Exception e)
            {
                Utils.LogError("CreateGenericMethod");
                Utils.LogError(e);
                throw;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public static object CreateBackendSessionAndTarkovApp(out object tarkovApp)
        {
            try
            {
                // To get to this point and keeping this generic
                // Get types required
                var singletonType = TypeHelper.GetSingletonType();
                var clientApplicationType = TypeHelper.GetClientApplicationType();
                var interfaceType = TypeHelper.GetInterfaceType();

                // Create singleton
                var clientApplicationInterfaceType = CreateGenericType(clientApplicationType, interfaceType);
                var singletonClientApplicationInterfaceType = CreateGenericType(singletonType, clientApplicationInterfaceType);

                // Get singleton instance
                var singletonClientApplicationInterfaceInstance = ReflectionHelper.GetSingletonInstance(singletonClientApplicationInterfaceType);

                tarkovApp = singletonClientApplicationInterfaceInstance;
                return ReflectionHelper.GetBackendSession(singletonClientApplicationInterfaceInstance);
            }
            catch (Exception e)
            {
                Utils.LogError("CreateBackendSessionAndTarkovApp");
                Utils.LogError(e);
                throw;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public static object GetWaveSettings()
        {
            try
            {
                // combine List<> and WaveSettingsType
                var listWaveType = CreateGenericType(TypeHelper.GetListType(), TypeHelper.GetWaveSettingsType());

                // combine with JsonConvert.DeserializeObject<>() and invoke with getCurrentDir + "\\DUMPDATA\\.replace("\\\\","\\") + "botReqData.json";
                return CreateDeserializerMethod(listWaveType)
                    .Invoke(null, new[] { File.ReadAllText(Path.Combine(DataHelper.DumpDataPath, "botReqData.json")) });
            }
            catch (Exception e)
            {
                Utils.LogError("GetWaveSettings");
                Utils.LogError(e);
                throw;
            }
        }

        public static object GetLocationValuesFromSession()
        {
            try
            {
                var locationsProp = DataHelper.Session.GetType().GetProperty("LocationSettings").GetValue(DataHelper.Session);
                return locationsProp.GetType().GetField("locations").GetValue(locationsProp);
            }
            catch (Exception e)
            {
                Utils.LogError("GetLocationValuesFromSession");
                Utils.LogError(e);
                throw;
            }
        }

        public static object CheckLocationID(string map)
        {
            try
            {
                var values = (IEnumerable<object>)DataHelper.LocationValues.GetType().GetProperty("Values").GetValue(DataHelper.LocationValues);
                return values.FirstOrDefault(x => x.GetType().GetField("Id").GetValue(x).ToString().ToLower().Contains(map.ToLower()));
            }
            catch (Exception e)
            {
                Utils.LogError("CheckLocationID");
                Utils.LogError(e);
                throw;
            }
        }

        public static object GetPlayerProfile()
        {
            try
            {
                var profile = DataHelper.Session.GetType().GetProperty("Profile").GetValue(DataHelper.Session);
                var converterMethod = CreateGenericMethod(MethodHelper.GetToUnparsedDataMethod(), TypeHelper.GetProfileType());
                return converterMethod.Invoke(null, new[] { profile, Array.Empty<JsonConverter>() });
            }
            catch (Exception e)
            {
                Utils.LogError("GetPlayerProfile");
                Utils.LogError(e);
                throw;
            }
        }
    }
}