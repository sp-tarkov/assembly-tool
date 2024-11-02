using System.Reflection;
using Newtonsoft.Json;
using DumpLib.Helpers;
using DumpLib.Models;

namespace DumpLib
{
    public static class DumpyTool
    {
        /// <summary>
        /// Method to run main menu Task, this will request data from BSG, map loot and bot data
        /// </summary>
        public static async Task StartDumpyTask()
        {
            if (!DataHelper.ConfigSettings.QuickDumpEnabled)
            {
                return;
            }

            Utils.LogInfo("[Dumpy] Starting Dumpy");

            await Task.Factory.StartNew(async delegate
            {
                bool run = true;
                while (run)
                {
                    try
                    {
                        SetupBackend();
                    }
                    catch (Exception e)
                    {
                        Utils.LogError("[Dumpy] Exception occured in SetupBackend");
                        Utils.LogError(e);

                        if (DataHelper.ErrorCounter >= 3)
                        {
                            Utils.LogError("[Dumpy] ErrorsCounter was 3, exiting app!");
                            MethodHelper.GetApplicationQuitMethod().Invoke(null, null);
                        }

                        DataHelper.ErrorCounter += 1;

                        Utils.LogError("[Dumpy] Resetting backend and trying again");
                        DataHelper.ClearVariables();
                        DataHelper.GotBackend = false;
                    }

                    if (DataHelper.GotBackend)
                    {
                        try
                        {
                            foreach (var map in DataHelper.ConfigSettings.MapNames)
                            {
                                // Set location in the RaidSettings object
                                Utils.LogInfo($"[Dumpy] Setting LocalRaidSettings location to: {map}");
                                DataHelper.LocalRaidSettings.GetType().GetField("location")
                                    .SetValue(DataHelper.LocalRaidSettings, map);

                                // Set location in the RaidConfig object
                                Utils.LogInfo($"[Dumpy] Setting RaidSettings location to: {map}");
                                DataHelper.RaidSettings.GetType().GetProperty("SelectedLocation")
                                    .SetValue(DataHelper.RaidSettings, ReflectionHelper.CheckLocationID(map));

                                // "/client/raid/configuration"
                                // Call server with new map name in RaidSettings
                                Utils.LogInfo($"[Dumpy] Sending RaidConfig");
                                await (Task)DataHelper.Session.GetType().GetMethod("SendRaidSettings")
                                    .Invoke(DataHelper.Session, new[] { DataHelper.RaidSettings });

                                // Artificial wait to hopefully keep BSG off our toes
                                Utils.LogInfo("Waiting 10s");
                                await Task.Delay(10000);

                                // "/client/match/local/start"
                                // Call server with new map name in LocalRaidSettings
                                Utils.LogInfo($"[Dumpy] Getting loot for {map}");
                                var localRaidSettings = DataHelper.Session.GetType().GetMethod("LocalRaidStarted")
                                    .Invoke(DataHelper.Session, new[] { DataHelper.LocalRaidSettings });
                                // Await the task
                                await (Task)localRaidSettings;
                                // Get the result
                                var result = localRaidSettings.GetType().GetProperty("Result").GetValue(localRaidSettings);
                                // get the string from the result
                                var result2 = (string)result.GetType().GetField("serverId").GetValue(result);
                                // set that to our LocalRaidSettings object
                                DataHelper.LocalRaidSettings.GetType().GetField("serverId").SetValue(DataHelper.LocalRaidSettings, result2);

                                // Artificial wait to hopefully keep BSG off our toes
                                Utils.LogInfo("Waiting 10s");
                                await Task.Delay(10000);

                                // "/client/game/bot/generate"
                                // Call server with bot wave data
                                Utils.LogInfo($"[Dumpy] Getting Bot Data");
                                await (Task)DataHelper.Session.GetType().GetMethod("LoadBots")
                                    .Invoke(DataHelper.Session, new[] { DataHelper.WaveSettings });

                                // Artificial wait to hopefully keep BSG off our toes
                                Utils.LogInfo("Waiting 10s");
                                await Task.Delay(10000);

                                var emptyArray = ReflectionHelper.CreateGenericMethod(typeof(Array).GetMethod("Empty"), TypeHelper.GetJsonTokenCreateType())
                                    .Invoke(null, null);
                                Utils.LogInfo($"[Dumpy] Sending local raid ended");
                                await (Task)DataHelper.Session.GetType().GetMethod("LocalRaidEnded")
                                    .Invoke(DataHelper.Session, new object[]
                                    {
                                        DataHelper.LocalRaidSettings,
                                        DataHelper.EndRaidClass,
                                        emptyArray,
                                        Activator.CreateInstance(ReflectionHelper.CreateGenericType(TypeHelper.GetDictionaryType(), typeof(string),
                                            emptyArray.GetType()))
                                    });

                                // after, reset LocalRaidSettings.serverId to null;
                                DataHelper.LocalRaidSettings.GetType().GetField("serverId").SetValue(DataHelper.LocalRaidSettings, null);

                                await Task.Delay(DataHelper.ConfigSettings.SptTimings.SingleIterationDelayMs);
                            }
                            
                            var controller = DataHelper.MainMenuController.GetValue(DataHelper.TarkovApp);
                            if (controller != null)
                            {
                                controller.GetType().GetMethod("StopAfkMonitor").Invoke(controller, null);
                            }

                            Utils.LogInfo($"[Dumpy] Restarting Loop in {DataHelper.ConfigSettings.SptTimings.AllIterationDelayMs}ms");
                            await Task.Delay(DataHelper.ConfigSettings.SptTimings.AllIterationDelayMs);
                        }
                        catch (Exception e)
                        {
                            Utils.LogError("[Dumpy] Exception occured in StartDumpyTask::Iteration");
                            Utils.LogError(e);

                            if (DataHelper.ErrorCounter >= 3)
                            {
                                Utils.LogError("[Dumpy] ErrorsCounter was 3, exiting app");
                                MethodHelper.GetApplicationQuitMethod().Invoke(null, null);
                            }

                            DataHelper.ErrorCounter += 1;

                            Utils.LogError("[Dumpy] Resetting backend and trying again");
                            DataHelper.ClearVariables();
                        }
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        private static void SetupBackend()
        {
            if (!DataHelper.GotBackend)
            {
                // get client backend session
                DataHelper.Session = ReflectionHelper.CreateBackendSessionAndTarkovApp(out DataHelper.TarkovApp);
                // get field for MainMenuController
                DataHelper.MainMenuController = ReflectionHelper.GetMainMenuControllerField();
                // get wave information from json
                DataHelper.WaveSettings = ReflectionHelper.GetWaveSettings();
                // get Raid Settings from json
                DataHelper.LocalRaidSettings = DataHelper.GetLocalRaidSettings();
                // get Raid Config from json
                DataHelper.RaidSettings = DataHelper.GetRaidConfigSettings();
                // get locationDetails
                DataHelper.LocationValues = ReflectionHelper.GetLocationValuesFromSession();
                // get End raid class from json
                DataHelper.EndRaidClass = DataHelper.GetEndRaidClass();
                // get player profile
                DataHelper.PlayerProfile = ReflectionHelper.GetProfileCompleteData();
                // Set up end raid class
                DataHelper.EndRaidClass.GetType().GetField("profile").SetValue(DataHelper.EndRaidClass, DataHelper.PlayerProfile);

                DataHelper.GotBackend = true;
            }
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
                var uri = new Uri((string)requestType.GetType().GetMethod("get_MainURLFull").Invoke(requestType, null));
                var path = (Directory.GetCurrentDirectory() + "\\HTTP_DATA\\").Replace("\\\\", "\\");
                var file = uri.LocalPath.Replace("/", ".").Remove(0, 1);
                var time = DateTime.Now.ToString(DataHelper.ConfigSettings.DateTimeFormat);

                if (Directory.CreateDirectory(path).Exists)
                {
                    var reqParams = requestType.GetType().GetField("Params").GetValue(requestType);
                    if (Directory.CreateDirectory($@"{path}req.{file}").Exists)
                    {
                        if (reqParams != null)
                        {
                            File.WriteAllText($@"{path}req.{file}\\req.{file}_{time}_{DataHelper.ConfigSettings.Name}.json",
                                JsonConvert.SerializeObject(reqParams));
                        }
                    }

                    if (Directory.CreateDirectory($@"{path}resp.{file}").Exists)
                    {
                        File.WriteAllText($@"{path}resp.{file}\\resp.{file}_{time}_{DataHelper.ConfigSettings.Name}.json", (string)responseText);
                    }
                }
            }
            catch (Exception e)
            {
                Utils.LogError("[Dumpy] Exception occured at LogRequestResponse");
                Utils.LogError(e);
                throw;
            }
        }
    }
}