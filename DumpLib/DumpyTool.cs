using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using DumpLib.Helpers;
using DumpLib.Models;

namespace DumpLib
{
    public static class DumpyTool
    {
        /// <summary>
        ///
        /// </summary>
        public static string DumpDataPath = (Directory.GetCurrentDirectory() + "\\DUMPDATA\\").Replace("\\\\", "\\");

        public static SptConfigClass ConfigSettings = (SptConfigClass)GetSptConfig();

        /// <summary>
        /// always start from 1 as their iterations are 1 to 6
        /// </summary>
        public static int Iteration = 1;

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
                UtilsHelper.LogError("CreateCombinedType");
                UtilsHelper.LogError(e);
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
                return ReflectionHelper.GetDeserializerMethodInfo().MakeGenericMethod(new Type[] { type as Type });
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("CreateCombinedMethod");
                UtilsHelper.LogError(e);
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
                var singletonType = ReflectionHelper.GetSingletonType();
                var clientApplicationType = ReflectionHelper.GetClientApplicationType();
                var interfaceType = ReflectionHelper.GetInterfaceType();

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
                UtilsHelper.LogError("CreateBackendSession");
                UtilsHelper.LogError(e);
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
                var listWaveType = CreateGenericType(ReflectionHelper.GetListType(), ReflectionHelper.GetWaveSettingsType());

                // combine with JsonConvert.DeserializeObject<>() and invoke with getCurrentDir + "\\DUMPDATA\\.replace("\\\\","\\") + "botReqData.json";
                return CreateDeserializerMethod(listWaveType).Invoke(null, new[] { File.ReadAllText(Path.Combine(DumpDataPath, "botReqData.json")) });
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("GetWaveSettings");
                UtilsHelper.LogError(e);
                throw;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public static object GetSptConfig()
        {
            try
            {
                return CreateDeserializerMethod(typeof(SptConfigClass)).Invoke(null,
                    new[] { File.ReadAllText(Path.Combine(DumpDataPath, "config.json")) });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static object GetRaidSettings()
        {
            try
            {
                return CreateDeserializerMethod(ReflectionHelper.GetLocalRaidSettingsType()).Invoke(null,
                    new[] { File.ReadAllText(Path.Combine(DumpDataPath, "raidSettings.json")) });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static bool GotBackend = false;
        public static object WaveSettings = null;
        public static object RaidSettings = null;
        public static object AppRaidSettings = null;
        public static FieldInfo MainMenuController = null;
        public static object Session = null;
        public static object TarkovApp = null;
        public static int ErrorCounter = 0;

        /// <summary>
        /// Method to run main menu Task, this will request data from BSG, map loot and bot data
        /// </summary>
        public static async Task StartDumpyTask()
        {
            if (!ConfigSettings.QuickDumpEnabled)
            {
                return;
            }

            await Task.Factory.StartNew(async delegate
            {
                UtilsHelper.LogInfo("[Dumpy] Starting Dumpy Loop");
                while (true)
                {
                    try
                    {
                        if (!GotBackend)
                        {
                            // get client backend session
                            Session = CreateBackendSessionAndTarkovApp(out TarkovApp);
                            // get field for MainMenuController
                            MainMenuController = ReflectionHelper.GetMainMenuControllerField();
                            // get wave information from json
                            WaveSettings = GetWaveSettings();
                            // get Raid Settings from json
                            RaidSettings = GetRaidSettings();
                            // get Raid settings from tarkovApp
                            AppRaidSettings = ReflectionHelper.GetRaidSettingsFromApp(TarkovApp);

                            CheckVariableConditions();
                            GotBackend = true;
                        }
                    }
                    catch (Exception e)
                    {
                        UtilsHelper.LogError("[Dumpy] Exception occured in StartDumpyTask::GotBackend");
                        UtilsHelper.LogError(e);

                        if (ErrorCounter > 3)
                        {
                            UtilsHelper.LogError("[Dumpy] ErrorsCounter was above 3, exiting app!");
                            // use EFT method to close app
                            ReflectionHelper.GetApplicationQuitMethod().Invoke(null, null);
                        }

                        ErrorCounter += 1;

                        UtilsHelper.LogError("[Dumpy] Resetting backend and trying again");
                        ClearVariables();
                    }

                    try
                    {
                        if (Iteration > 6)
                        {
                            // reset to 1
                            Iteration = 1;

                            UtilsHelper.LogInfo($"[Dumpy] Restarting Loop in {ConfigSettings.SptTimings.AllIterationDelayMs}ms");
                            var controller = MainMenuController.GetValue(TarkovApp);

                            if (controller != null)
                            {
                                controller.GetType().GetMethod("StopAfkMonitor").Invoke(controller, null);
                            }

                            await Task.Delay(ConfigSettings.SptTimings.AllIterationDelayMs);
                        }
                        else
                        {
                            UtilsHelper.LogInfo($"Map iteration number: {Iteration}");
                            foreach (var map in ConfigSettings.MapNames)
                            {
                                // theory is send a request SendRaidSettings before starting

                                // Set location in the RaidSettings object
                                UtilsHelper.LogInfo($"[Dumpy] Setting RaidSettings location to: {map}");
                                RaidSettings.GetType().GetField("location").SetValue(RaidSettings, map);

                                // Call server with new map name
                                UtilsHelper.LogInfo($"[Dumpy] Getting loot for {map}");
                                await (Task)Session.GetType().GetMethod("LocalRaidStarted")
                                    .Invoke(Session, new[] { RaidSettings });

                                // Call server with bot wave data
                                UtilsHelper.LogInfo($"[Dumpy] Getting Bot Data");
                                await (Task)Session.GetType().GetMethod("LoadBots")
                                    .Invoke(Session, new[] { WaveSettings });

                                await Task.Delay(ConfigSettings.SptTimings.SingleIterationDelayMs);
                            }

                            Iteration++;
                        }
                    }
                    catch (Exception e)
                    {
                        UtilsHelper.LogError("[Dumpy] Exception occured in StartDumpyTask::Iteration");
                        UtilsHelper.LogError(e);

                        if (ErrorCounter > 3)
                        {
                            UtilsHelper.LogError("[Dumpy] ErrorsCounter was above 3, exiting app");
                            // use EFT method to close app
                            ReflectionHelper.GetApplicationQuitMethod().Invoke(null, null);
                        }

                        ErrorCounter += 1;

                        UtilsHelper.LogError("[Dumpy] Resetting backend and trying again");
                        ClearVariables();
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        private static void CheckVariableConditions()
        {
            UtilsHelper.LogInfo($"[Dumpy] CheckVariableConditions");
            UtilsHelper.LogInfo($"[Dumpy] GotBackend- type: {GotBackend.GetType()} null?: {GotBackend == null}");
            UtilsHelper.LogInfo($"[Dumpy] WaveSettings- type: {WaveSettings.GetType()} null?: {WaveSettings == null}");
            UtilsHelper.LogInfo($"[Dumpy] MainMenuController- type: {MainMenuController.GetType()} null?: {MainMenuController == null}");
            UtilsHelper.LogInfo($"[Dumpy] Session- type: {Session.GetType()} null?: {Session == null}");
            UtilsHelper.LogInfo($"[Dumpy] TarkovApp- type: {TarkovApp.GetType()} null?: {TarkovApp == null}");
            UtilsHelper.LogInfo($"[Dumpy] RaidSettings- type: {RaidSettings.GetType()} null?: {RaidSettings == null}");
            UtilsHelper.LogInfo($"[Dumpy] CheckVariableConditions");
            UtilsHelper.LogInfo($"[Dumpy] AppRaidSettings- type: {AppRaidSettings.GetType()} null?: {AppRaidSettings == null}");
            UtilsHelper.LogInfo($"[Dumpy] CheckVariableConditions");
            UtilsHelper.LogInfo($"[Dumpy] -----------------------------------------------------------------------------");
        }

        private static void ClearVariables()
        {
            GotBackend = false;
            WaveSettings = null;
            MainMenuController = null;
            Session = null;
            TarkovApp = null;
            RaidSettings = null;
            AppRaidSettings = null;
        }

        /// <summary>
        /// Method to log Requests and Responses
        /// </summary>
        /// <param name="requestType">object (Type)</param>
        /// <param name="responseType">object (Type)</param>
        public static void LogRequestResponse(object requestType, object responseText)
        {
            try
            {
                var uri = new Uri((string)requestType.GetType().GetMethod(ConfigSettings.SptReflections.MainUrlPropName).Invoke(requestType, null));
                var path = (Directory.GetCurrentDirectory() + "\\HTTP_DATA\\").Replace("\\\\", "\\");
                var file = uri.LocalPath.Replace("/", ".").Remove(0, 1);
                var time = DateTime.Now.ToString(ConfigSettings.DateTimeFormat);

                if (Directory.CreateDirectory(path).Exists)
                {
                    var reqParams = requestType.GetType().GetField(ConfigSettings.SptReflections.ParamFieldName).GetValue(requestType);
                    if (Directory.CreateDirectory($@"{path}req.{file}").Exists)
                    {
                        if (reqParams != null)
                            File.WriteAllText($@"{path}req.{file}\\req.{file}_{time}_{ConfigSettings.Name}.json", JsonConvert.SerializeObject(reqParams));
                    }

                    if (Directory.CreateDirectory($@"{path}resp.{file}").Exists)
                        File.WriteAllText($@"{path}resp.{file}\\resp.{file}_{time}_{ConfigSettings.Name}.json", (string)responseText);
                }
            }
            catch (Exception e)
            {
                UtilsHelper.LogError("[Dumpy] Exception occured at LogRequestResponse");
                UtilsHelper.LogError(e);
                throw;
            }
        }
    }
}