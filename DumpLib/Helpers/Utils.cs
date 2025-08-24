using System;
using System.IO;

namespace DumpLib.Helpers
{
    public static class Utils
    {
        private static string _loggerPath = (Directory.GetCurrentDirectory() + "\\DUMPDATA\\Log.txt").Replace(
            "\\\\",
            "\\"
        );

        /// <summary>
        /// Log message to something
        /// </summary>
        /// <param name="message">object</param>
        private static void LogMessage(object message, string messageType)
        {
            StreamWriter writer = null;
            try
            {
                writer = new StreamWriter(_loggerPath, true);
                writer.WriteLine($"[{messageType}] - {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                if (writer != null)
                {
                    writer.Close();
                    writer.Dispose();
                }
            }
        }

        /// <summary>
        /// Log message to something
        /// </summary>
        /// <param name="message">object</param>
        public static void LogError(object message)
        {
            LogMessage(message, "Error");
        }

        /// <summary>
        /// Log message to something
        /// </summary>
        /// <param name="message">object</param>
        public static void LogInfo(object message)
        {
            LogMessage(message, "Info");
        }

        /// <summary>
        /// Log message to something
        /// </summary>
        /// <param name="message">object</param>
        public static void LogDebug(object message)
        {
            LogMessage(message, "Debug");
        }
    }
}
