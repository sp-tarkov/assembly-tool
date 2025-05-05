namespace DumpLib.Models
{
    public class SptConfigClass
    {
        /// <summary>
        /// Default: Test
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        ///  Default: [ "Interchange", "factory4_day", "laboratory", "bigmap", "Lighthouse", "RezervBase", "Sandbox", "Shoreline", "TarkovStreets", "Woods" ]
        /// </summary>
        public string[] MapNames { get; set; }
        
        /// <summary>
        /// Default: "yyyy-MM-dd_HH-mm-ss"
        /// </summary>
        public string DateTimeFormat { get; set; }

        /// <summary>
        /// Default: False
        /// </summary>
        public bool QuickDumpEnabled { get; set; }
        
        public SptTimings SptTimings { get; set; }
        
        public bool EnableCustomDumpPath { get; set; }
        
        public string CustomDumpPath { get; set; }
    }

    public class SptTimings
    {
        /// <summary>
        /// Default: 10s * 1000ms = 10000ms
        /// </summary>
        public int SingleIterationDelayMs { get; set; }
        
        /// <summary>
        /// Default: 5m * 60s * 1000ms = 300000ms
        /// </summary>
        public int AllIterationDelayMs { get; set; }
        
        /// <summary>
        /// Default: 10 retries
        /// </summary>
        public int RetriesBeforeQuit { get; set; }
        
        /// <summary>
        /// Default: 1m * 60s * 1000ms = 60000ms
        /// </summary>
        public int RetryDelayMs { get; set; }
    }
}